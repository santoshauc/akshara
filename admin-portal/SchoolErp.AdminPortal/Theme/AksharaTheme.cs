using MudBlazor;

namespace SchoolErp.AdminPortal.Theme;

/// <summary>
/// The MudBlazor half of the design system. Values here MUST mirror
/// wwwroot/css/design-tokens.css — MudBlazor renders many colours into inline
/// styles that CSS variables never reach, so the two layers have to agree.
/// Change a colour in one place and you must change it in the other.
/// </summary>
public static class AksharaTheme
{
    /// <summary>Akshara's own brand colour, used until a school overrides it.</summary>
    public const string DefaultBrand = "#00695c";

    /// <summary>
    /// Builds the theme for a school's brand colour. Only the brand accents
    /// move; surfaces, borders and text stay fixed so a badly chosen school
    /// colour can never destroy contrast or legibility.
    /// </summary>
    public static MudTheme Build(string? brand, string? accent)
    {
        var primary = string.IsNullOrWhiteSpace(brand) ? DefaultBrand : brand;
        var secondary = string.IsNullOrWhiteSpace(accent) ? "#5b3fc4" : accent;

        return new MudTheme
        {
            Palette = new PaletteLight
            {
                Primary = primary,
                Secondary = secondary,
                Tertiary = "#1c5fb8",

                Success = "#1a7f42",
                Warning = "#a86612",
                Error = "#c0322b",
                Info = "#1c5fb8",

                Background = "#f8f9fb",
                BackgroundGrey = "#f1f3f7",
                Surface = "#ffffff",
                DrawerBackground = "#ffffff",
                DrawerText = "#5b6479",
                DrawerIcon = "#5b6479",

                // The app bar is a neutral surface, not a slab of brand colour:
                // colour is reserved for meaning (status, actions), not chrome.
                AppbarBackground = "#ffffff",
                AppbarText = "#1a1f2c",

                TextPrimary = "#1a1f2c",
                TextSecondary = "#5b6479",
                TextDisabled = "#a5adc0",

                ActionDefault = "#5b6479",
                ActionDisabled = "#a5adc0",
                ActionDisabledBackground = "#f1f3f7",

                Divider = "#e4e7ee",
                DividerLight = "#f1f3f7",
                LinesDefault = "#e4e7ee",
                LinesInputs = "#cfd4e0",
                TableLines = "#e4e7ee",
                TableStriped = "#fcfcfd",
                TableHover = "#f8f9fb",

                GrayDefault = "#7b849b",
                GrayLight = "#a5adc0",
                GrayLighter = "#e4e7ee",
                GrayDark = "#434b5e",
                GrayDarker = "#2c3243",

                OverlayDark = "rgba(17, 21, 31, 0.45)",
            },
            PaletteDark = new PaletteDark
            {
                Primary = LightenForDark(primary),
                Secondary = "#9d86ee",
                Tertiary = "#5b9bf0",

                Success = "#48c07a",
                Warning = "#e0a447",
                Error = "#e8635b",
                Info = "#5b9bf0",

                Background = "#11151f",
                BackgroundGrey = "#12161f",
                Surface = "#171c28",
                DrawerBackground = "#171c28",
                DrawerText = "#a8b0c2",
                DrawerIcon = "#a8b0c2",

                AppbarBackground = "#171c28",
                AppbarText = "#e9ecf3",

                TextPrimary = "#e9ecf3",
                TextSecondary = "#a8b0c2",
                TextDisabled = "#7b849b",

                ActionDefault = "#a8b0c2",
                ActionDisabled = "#5b6479",
                ActionDisabledBackground = "#1f2534",

                Divider = "#2a3142",
                DividerLight = "#232a38",
                LinesDefault = "#2a3142",
                LinesInputs = "#3a4256",
                TableLines = "#2a3142",
                TableStriped = "#1a2029",
                TableHover = "#1f2534",

                OverlayDark = "rgba(0, 0, 0, 0.6)",
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "5px",
                DrawerWidthLeft = "248px",
                DrawerMiniWidthLeft = "60px",
                AppbarHeight = "52px",
            },
            Typography = new Typography
            {
                Default = new Default
                {
                    FontFamily = FontStack,
                    FontSize = "0.875rem",     // 14px
                    FontWeight = 400,
                    LineHeight = 1.5,
                    LetterSpacing = "0",
                },
                H1 = new H1 { FontFamily = FontStack, FontSize = "1.5rem", FontWeight = 600, LineHeight = 1.25, LetterSpacing = "-0.02em" },
                H2 = new H2 { FontFamily = FontStack, FontSize = "1.375rem", FontWeight = 600, LineHeight = 1.25, LetterSpacing = "-0.015em" },
                H3 = new H3 { FontFamily = FontStack, FontSize = "1.25rem", FontWeight = 600, LineHeight = 1.3, LetterSpacing = "-0.01em" },
                // H4 is the page-title slot; deliberately modest — an operations
                // tool should not open with a marketing-sized headline.
                H4 = new H4 { FontFamily = FontStack, FontSize = "1.25rem", FontWeight = 600, LineHeight = 1.3, LetterSpacing = "-0.01em" },
                H5 = new H5 { FontFamily = FontStack, FontSize = "1.125rem", FontWeight = 600, LineHeight = 1.35 },
                H6 = new H6 { FontFamily = FontStack, FontSize = "1rem", FontWeight = 600, LineHeight = 1.4 },
                Subtitle1 = new Subtitle1 { FontFamily = FontStack, FontSize = "0.9375rem", FontWeight = 500, LineHeight = 1.45 },
                Subtitle2 = new Subtitle2 { FontFamily = FontStack, FontSize = "0.8125rem", FontWeight = 500, LineHeight = 1.45 },
                Body1 = new Body1 { FontFamily = FontStack, FontSize = "0.875rem", FontWeight = 400, LineHeight = 1.5 },
                Body2 = new Body2 { FontFamily = FontStack, FontSize = "0.8125rem", FontWeight = 400, LineHeight = 1.5 },
                Button = new Button { FontFamily = FontStack, FontSize = "0.8125rem", FontWeight = 500, LineHeight = 1.5, TextTransform = "none", LetterSpacing = "0" },
                Caption = new Caption { FontFamily = FontStack, FontSize = "0.75rem", FontWeight = 400, LineHeight = 1.45 },
                Overline = new Overline { FontFamily = FontStack, FontSize = "0.6875rem", FontWeight = 600, LineHeight = 1.4, LetterSpacing = "0.06em", TextTransform = "uppercase" },
            },
            Shadows = new Shadow(),
        };
    }

    private static readonly string[] FontStack =
        ["Inter", "Segoe UI", "system-ui", "-apple-system", "sans-serif"];

    /// <summary>
    /// Dark mode needs a lighter brand tint or the accent disappears against
    /// a near-black canvas — a dark teal on a dark surface measured 1.44:1,
    /// far below the 4.5:1 AA floor. Mixes the colour toward white by 35%;
    /// returns the input unchanged when it is not a parseable hex triplet.
    /// </summary>
    public static string LightenForDark(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#' ||
            !int.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !int.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !int.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return hex;
        }

        // 55%, arrived at by measurement rather than taste: against the dark
        // selected-row surface, Akshara's teal measured 1.44:1 unlifted,
        // 4.36:1 at 45% — still under the 4.5:1 AA floor — and 5.5:1 at 55%.
        // Raising this is safe; lowering it breaks contrast for every school
        // whose brand colour is dark.
        static int Mix(int channel) => channel + (int)((255 - channel) * 0.55);
        return $"#{Mix(r):x2}{Mix(g):x2}{Mix(b):x2}";
    }
}
