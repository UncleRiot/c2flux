using System.Drawing;
using System.Windows.Forms;

namespace c2flux
{
    public static class DialogTableStyle
    {
        public const int HorizontalMargin = AntdThemeService.HorizontalMargin;

        public static Padding CreateHorizontalPadding(int top, int bottom)
        {
            return AntdThemeService.CreateHorizontalPadding(top, bottom);
        }

        public static Panel CreateTableHost(
            Control content,
            Color backColor,
            int top,
            int bottom)
        {
            return AntdThemeService.CreateTableHost(
                content,
                backColor,
                top,
                bottom);
        }

        public static void ConfigureTablePage(TabPage tabPage, Color backColor)
        {
            AntdThemeService.ConfigureTablePage(tabPage, backColor);
        }

        public static void Apply(DataGridView grid)
        {
            AntdThemeService.ApplyTable(grid);
        }

        public static void ApplyDetails(DataGridView grid)
        {
            AntdThemeService.ApplyDetailsTable(grid);
        }
    }
}
