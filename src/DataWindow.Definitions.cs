using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ModbusGetFree
{
    public partial class DataWindow
    {
        public int RegisterStride { get { return EnronActive ? 2 : 1; } }

        RegisterDefinition DefaultDefinition(int index)
        {
            return new RegisterDefinition
            {
                Address = State.Address + index / RegisterStride,
                Format = State.Format,
                Order = State.Order,
                Scale = State.Scale,
                Offset = State.Offset,
                Foreground = State.Foreground,
                Background = State.Background,
                ColorBelow = State.ColorBelow,
                ColorAbove = State.ColorAbove,
                LowColor = State.LowColor,
                HighColor = State.HighColor
            };
        }

        public int GroupWidth(RegisterDefinition definition)
        {
            return IsBitArea ? 1 : Math.Max(RegisterStride, ValueCodec.Width(definition.Format));
        }

        public bool IsBitArea
        {
            get { return State.Function <= 2 || State.Function == 5 || State.Function == 15; }
        }

        int[] groupOwners;
        readonly Dictionary<int, RegisterDefinition> resolvedGroups = new Dictionary<int, RegisterDefinition>();
        readonly Dictionary<string, System.Drawing.Font> groupFonts = new Dictionary<string, System.Drawing.Font>();

        void InvalidateGroupMap()
        {
            groupOwners = null;
        }

        void EnsureGroupMap()
        {
            if (groupOwners != null && groupOwners.Length == StorageQuantity)
            {
                return;
            }

            if (State.Definitions == null)
            {
                State.Definitions = new List<RegisterDefinition>();
            }

            // 旧文件只有窗口级格式：首次加载时展开为组定义，后续只修改所选组。
            if (State.Definitions.Count == 0 && !IsBitArea && ValueCodec.Width(State.Format) > RegisterStride)
            {
                int legacyWidth = ValueCodec.Width(State.Format);
                for (int start = 0; start < StorageQuantity; start += legacyWidth)
                {
                    State.Definitions.Add(DefaultDefinition(start));
                }

                if (!EnronActive)
                {
                    State.Format = 0;
                }
            }

            groupOwners = new int[StorageQuantity];
            resolvedGroups.Clear();
            for (int start = 0; start < StorageQuantity;)
            {
                var definition = ResolveDefinition(start);
                resolvedGroups[start] = definition;
                int end = Math.Min(StorageQuantity, start + GroupWidth(definition));
                for (int index = start; index < end; index++)
                {
                    groupOwners[index] = start;
                }

                start = end;
            }
        }

        public IEnumerable<int> GroupStarts()
        {
            EnsureGroupMap();
            return resolvedGroups.Keys.OrderBy(index => index);
        }

        System.Drawing.Font GroupFont(RegisterDefinition definition)
        {
            string key = definition.FontName + ":" + definition.FontSize + ":" + definition.FontStyle;
            System.Drawing.Font font;
            if (!groupFonts.TryGetValue(key, out font))
            {
                font = new System.Drawing.Font(definition.FontName, definition.FontSize,
                    (System.Drawing.FontStyle)definition.FontStyle);
                groupFonts[key] = font;
            }

            return font;
        }

        RegisterDefinition DefinitionAtStart(int index)
        {
            EnsureGroupMap();
            return resolvedGroups[GroupStart(index)];
        }

        RegisterDefinition ResolveDefinition(int index)
        {
            int address = State.Address + index / RegisterStride;
            var custom = State.Definitions.FirstOrDefault(definition => definition.Address == address);
            if (custom != null)
            {
                return custom;
            }

            var fallback = DefaultDefinition(index);
            int end = address + GroupWidth(fallback) / RegisterStride;
            if (State.Definitions.Any(definition => definition.Address > address && definition.Address < end))
            {
                fallback.Format = EnronActive ? 4 : 0;
            }

            return fallback;
        }

        public int GroupStart(int index)
        {
            if (index < 0 || index >= StorageQuantity)
            {
                return index;
            }

            EnsureGroupMap();
            return groupOwners[index];
        }

        public RegisterDefinition DefinitionAt(int index)
        {
            if (index < 0 || index >= StorageQuantity)
            {
                return DefaultDefinition(0);
            }

            return DefinitionAtStart(GroupStart(index));
        }

        public RegisterDefinition SelectedDefinition
        {
            get { return DefinitionAt(SelectedRawIndex); }
        }

        public int[] SelectedGroupStarts()
        {
            var cells = Grid.SelectedCells.Cast<DataGridViewCell>().ToArray();
            var valueCells = cells.Where(cell => cell.ColumnIndex % 2 == 1).ToArray();
            if (valueCells.Length > 0)
            {
                cells = valueCells;
            }

            var indices = cells.Select(cell => Index(cell.RowIndex, cell.ColumnIndex))
                .Where(index => index >= 0 && index < StorageQuantity)
                .Select(GroupStart).Distinct().OrderBy(index => index).ToArray();
            return indices.Length == 0 ? new[] { SelectedRawIndex } : indices;
        }

        // 修改采用事务式准备：先检查所有跨度，再写入定义，避免部分修改后才发现越界。
        public void SetGroupFormat(int[] starts, int format, int? order = null)
        {
            if (format < 0 || format >= ValueCodec.Formats.Length ||
                (order.HasValue && (order.Value < 0 || order.Value > 3)))
            {
                throw new ArgumentException("Invalid display format / byte order.");
            }

            if (EnronActive && ValueCodec.Width(format) != 2)
            {
                throw new ArgumentException("Enron 32-bit ranges require a 32-bit display format.");
            }

            if (IsBitArea && format > 1)
            {
                throw new ArgumentException("Coil and discrete input areas contain single bits.");
            }

            var definitions = new List<RegisterDefinition>();
            int coveredUntil = -1;
            foreach (int start in starts.Select(GroupStart).Distinct().OrderBy(index => index))
            {
                if (start < coveredUntil)
                {
                    continue;
                }

                var definition = DefinitionAt(start).Copy();
                definition.Address = State.Address + start / RegisterStride;
                definition.Format = format;
                if (order.HasValue)
                {
                    definition.Order = order.Value;
                }

                int width = GroupWidth(definition);
                if (start < 0 || start + width > StorageQuantity)
                {
                    throw new ArgumentException("The selected value needs more registers than this read area contains.");
                }

                definitions.Add(definition);
                coveredUntil = start + width;
            }

            foreach (var definition in definitions)
            {
                int end = definition.Address + GroupWidth(definition) / RegisterStride;
                State.Definitions.RemoveAll(existing => existing.Address < end &&
                    existing.Address + GroupWidth(existing) / RegisterStride > definition.Address);
                State.Definitions.Add(definition);
            }

            InvalidateGroupMap();
            RefreshValues();
            host.MarkDirty();
        }

        public void UpdateSelectedDefinitions(Action<RegisterDefinition> update)
        {
            var definitions = SelectedGroupStarts().Select(index => DefinitionAt(index).Copy()).ToArray();
            foreach (var definition in definitions)
            {
                update(definition);
            }

            foreach (var definition in definitions)
            {
                State.Definitions.RemoveAll(existing => existing.Address == definition.Address);
                State.Definitions.Add(definition);
            }

            InvalidateGroupMap();
            RefreshValues();
            host.MarkDirty();
        }

        public string DisplayGroup(int index) { return FormatGroup(index, true); }

        public string FormatGroup(int index) { return FormatGroup(index, false); }

        string FormatGroup(int index, bool display)
        {
            if (index < 0 || index >= StorageQuantity)
            {
                return "";
            }

            if (GroupStart(index) != index)
            {
                return "--";
            }

            if (IsBitArea)
            {
                return State.Values[index].ToString();
            }

            var definition = DefinitionAtStart(index);
            return display ? ValueCodec.Display(State.Values, index, definition.Format, definition.Order,
                definition.Scale, definition.Offset) : ValueCodec.Format(State.Values, index, definition.Format,
                definition.Order, definition.Scale, definition.Offset);
        }

        public ushort[] EncodeGroup(int index, string text)
        {
            index = GroupStart(index);
            var definition = DefinitionAt(index);
            return IsBitArea ? new[] { ValueCodec.ParseWord(text) } :
                ValueCodec.Encode(text, definition.Format, definition.Order, definition.Scale, definition.Offset);
        }
    }
}
