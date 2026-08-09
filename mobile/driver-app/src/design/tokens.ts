/**
 * Akshara mobile design tokens — the shared visual language for the Parent
 * and Driver applications.
 *
 * The two apps are different products with different jobs (reassurance vs
 * operation), but they must look like they came from the same company. This
 * file is that guarantee: both apps import it, neither hard-codes a colour.
 *
 * Kept deliberately parallel to the portal's design-tokens.css so the family
 * resemblance survives across web and mobile — but the scales are re-tuned
 * for touch: bigger type, bigger targets, more generous spacing.
 *
 * NOTE: this file is duplicated verbatim in parent-app/src/design/tokens.ts.
 * The two Expo apps have separate Metro roots, and wiring a shared package
 * across them costs more in build fragility than the duplication costs in
 * maintenance. Change one, change the other.
 */

export const palette = {
  /* Neutral ramp — the backbone. */
  white: '#ffffff',
  grey25: '#fcfcfd',
  grey50: '#f7f8fa',
  grey100: '#eff1f5',
  grey200: '#e2e5ec',
  grey300: '#ccd1dc',
  grey400: '#a2aab9',
  grey500: '#78808f',
  grey600: '#586071',
  grey700: '#3f4657',
  grey800: '#282e3c',
  grey900: '#161b26',

  /* Brand — Akshara's teal. Calm rather than loud: this app carries news
     about somebody's child, so it should never feel like a game. */
  brand: '#00695c',
  brandDark: '#004d43',
  brandSurface: '#e3f1ef',
  brandOn: '#ffffff',

  /* Semantic. Each has a surface for banners, a solid for icons/fills, and
     a text tone that clears 4.5:1 on its own surface. */
  successSurface: '#e6f5ec',
  success: '#137a42',
  successText: '#0f5c32',

  warningSurface: '#fdf2e0',
  warning: '#a35d0a',
  warningText: '#7a4507',

  dangerSurface: '#fdecea',
  danger: '#c62828',
  dangerText: '#8e1f1f',

  infoSurface: '#e8f1fd',
  info: '#1c5fb8',
  infoText: '#14458a',
} as const;

/**
 * 4pt grid. Touch UIs need more air than dense web tables, so screens are
 * built mostly from 12/16/20/24.
 */
export const space = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
} as const;

export const radius = {
  sm: 6,
  md: 10,
  lg: 14,
  xl: 20,
  pill: 999,
} as const;

/**
 * Type scale. Minimum body size is 15 — anything smaller is unreadable at
 * arm's length on a phone in daylight, which is exactly when a parent checks
 * whether their child got on the bus.
 */
export const type = {
  display: { fontSize: 30, lineHeight: 36, fontWeight: '700' as const, letterSpacing: -0.4 },
  title: { fontSize: 22, lineHeight: 28, fontWeight: '700' as const, letterSpacing: -0.2 },
  heading: { fontSize: 18, lineHeight: 24, fontWeight: '600' as const },
  subheading: { fontSize: 16, lineHeight: 22, fontWeight: '600' as const },
  body: { fontSize: 15, lineHeight: 21, fontWeight: '400' as const },
  bodyStrong: { fontSize: 15, lineHeight: 21, fontWeight: '600' as const },
  label: { fontSize: 13, lineHeight: 18, fontWeight: '500' as const },
  caption: { fontSize: 12, lineHeight: 16, fontWeight: '400' as const },
  overline: {
    fontSize: 11,
    lineHeight: 14,
    fontWeight: '700' as const,
    letterSpacing: 0.6,
    textTransform: 'uppercase' as const,
  },
} as const;

/**
 * Elevation. Mobile surfaces are mostly flat with hairlines; shadow is
 * reserved for things that genuinely float above content.
 */
export const elevation = {
  none: {},
  card: {
    shadowColor: '#0b1020',
    shadowOpacity: 0.06,
    shadowRadius: 8,
    shadowOffset: { width: 0, height: 2 },
    elevation: 2,
  },
  sheet: {
    shadowColor: '#0b1020',
    shadowOpacity: 0.16,
    shadowRadius: 20,
    shadowOffset: { width: 0, height: -4 },
    elevation: 12,
  },
} as const;

/**
 * Minimum interactive size. 48 rather than the 44 minimum, because the
 * driver app is used one-handed in a vehicle and the parent app is used
 * while walking.
 */
export const touch = {
  minTarget: 48,
  primaryHeight: 56,
} as const;

export const semantic = {
  screen: palette.grey50,
  surface: palette.white,
  surfaceSunken: palette.grey100,
  border: palette.grey200,
  borderStrong: palette.grey300,
  text: palette.grey900,
  textSecondary: palette.grey600,
  textMuted: palette.grey500,
  textOnBrand: palette.brandOn,
} as const;
