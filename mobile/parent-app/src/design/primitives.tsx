import React from 'react';
import {
  AccessibilityRole,
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
  ViewStyle,
} from 'react-native';
import { elevation, palette, radius, semantic, space, touch, type } from './tokens';

/**
 * Shared mobile primitives. Screens compose these; screens do not invent
 * their own button, card or status styling. Duplicated verbatim in the
 * driver app — see the note in tokens.ts.
 */

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'brand';

const toneColours: Record<Tone, { surface: string; text: string; solid: string }> = {
  neutral: { surface: palette.grey100, text: palette.grey700, solid: palette.grey500 },
  success: { surface: palette.successSurface, text: palette.successText, solid: palette.success },
  warning: { surface: palette.warningSurface, text: palette.warningText, solid: palette.warning },
  danger: { surface: palette.dangerSurface, text: palette.dangerText, solid: palette.danger },
  info: { surface: palette.infoSurface, text: palette.infoText, solid: palette.info },
  brand: { surface: palette.brandSurface, text: palette.brandDark, solid: palette.brand },
};

/** A titled surface. Hairline border, not a drop shadow, unless it floats. */
export function Card({
  children,
  style,
  padded = true,
}: {
  children: React.ReactNode;
  style?: ViewStyle;
  padded?: boolean;
}) {
  return (
    <View style={[styles.card, padded && { padding: space.lg }, style]}>{children}</View>
  );
}

/** Section label above a group of content. */
export function SectionLabel({ children }: { children: React.ReactNode }) {
  return <Text style={styles.sectionLabel}>{children}</Text>;
}

/**
 * Status indicator. Always renders a glyph, a label AND a colour so state is
 * never carried by colour alone — a requirement for colour-blind users and
 * for anyone glancing at a phone in bright sun.
 */
export function StatusPill({
  label,
  tone = 'neutral',
  glyph,
}: {
  label: string;
  tone?: Tone;
  glyph?: string;
}) {
  const colours = toneColours[tone];
  return (
    <View
      accessibilityRole="text"
      accessibilityLabel={label}
      style={[styles.pill, { backgroundColor: colours.surface }]}
    >
      {glyph ? (
        <Text style={[styles.pillGlyph, { color: colours.text }]} accessible={false}>
          {glyph}
        </Text>
      ) : null}
      <Text style={[styles.pillText, { color: colours.text }]}>{label}</Text>
    </View>
  );
}

/**
 * The screen's primary action. Deliberately tall and full-width: on a phone
 * the main action should be unmissable and reachable with a thumb.
 */
export function PrimaryButton({
  label,
  onPress,
  disabled,
  loading,
  tone = 'brand',
  accessibilityHint,
}: {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
  tone?: 'brand' | 'danger';
  accessibilityHint?: string;
}) {
  const background = tone === 'danger' ? palette.danger : palette.brand;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: !!disabled || !!loading, busy: !!loading }}
      disabled={disabled || loading}
      onPress={onPress}
      style={({ pressed }) => [
        styles.primaryButton,
        { backgroundColor: background },
        pressed && styles.pressed,
        (disabled || loading) && styles.disabled,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={palette.brandOn} />
      ) : (
        <Text style={styles.primaryButtonLabel}>{label}</Text>
      )}
    </Pressable>
  );
}

/** Secondary action — same height, quieter treatment. */
export function SecondaryButton({
  label,
  onPress,
  disabled,
}: {
  label: string;
  onPress: () => void;
  disabled?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: !!disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.secondaryButton,
        pressed && styles.pressed,
        disabled && styles.disabled,
      ]}
    >
      <Text style={styles.secondaryButtonLabel}>{label}</Text>
    </Pressable>
  );
}

/** A tappable row. Minimum 48pt tall so it is comfortable while walking. */
export function ListRow({
  title,
  subtitle,
  trailing,
  onPress,
  accessibilityRole = 'button',
}: {
  title: string;
  subtitle?: string;
  trailing?: React.ReactNode;
  onPress?: () => void;
  accessibilityRole?: AccessibilityRole;
}) {
  const content = (
    <View style={styles.row}>
      <View style={{ flex: 1 }}>
        <Text style={styles.rowTitle}>{title}</Text>
        {subtitle ? <Text style={styles.rowSubtitle}>{subtitle}</Text> : null}
      </View>
      {trailing}
    </View>
  );

  if (!onPress) {
    return content;
  }

  return (
    <Pressable
      accessibilityRole={accessibilityRole}
      accessibilityLabel={subtitle ? `${title}. ${subtitle}` : title}
      onPress={onPress}
      style={({ pressed }) => [pressed && styles.pressed]}
    >
      {content}
    </Pressable>
  );
}

/** Shape-matched placeholder so the screen does not jump when data lands. */
export function Skeleton({ height = 16, width }: { height?: number; width?: number | string }) {
  return (
    <View
      accessible={false}
      style={[styles.skeleton, { height, width: (width as number) ?? '100%' }]}
    />
  );
}

/** Never a blank screen: says what is loading, in words. */
export function LoadingState({ message }: { message: string }) {
  return (
    <View style={styles.state} accessibilityLiveRegion="polite">
      <ActivityIndicator color={palette.brand} />
      <Text style={styles.stateBody}>{message}</Text>
    </View>
  );
}

/** Says what is missing and what to do about it — never just "no data". */
export function EmptyState({ title, body }: { title: string; body?: string }) {
  return (
    <View style={styles.state}>
      <Text style={styles.stateTitle}>{title}</Text>
      {body ? <Text style={styles.stateBody}>{body}</Text> : null}
    </View>
  );
}

/**
 * Human-readable failure with a way out. Status codes and stack traces never
 * reach a parent or a driver.
 */
export function ErrorState({
  title,
  body,
  retryLabel,
  onRetry,
}: {
  title: string;
  body: string;
  retryLabel: string;
  onRetry: () => void;
}) {
  return (
    <View style={styles.state} accessibilityLiveRegion="assertive">
      <Text style={styles.stateTitle}>{title}</Text>
      <Text style={styles.stateBody}>{body}</Text>
      <View style={{ marginTop: space.lg, alignSelf: 'stretch' }}>
        <SecondaryButton label={retryLabel} onPress={onRetry} />
      </View>
    </View>
  );
}

/**
 * Offline notice. Framed as information, not failure — the driver app keeps
 * working offline by design, so this must not read as an error.
 */
export function OfflineBanner({ message }: { message: string }) {
  return (
    <View style={styles.offline} accessibilityLiveRegion="polite">
      <Text style={styles.offlineText}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: semantic.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: semantic.border,
    ...elevation.card,
  },
  sectionLabel: {
    ...type.overline,
    color: semantic.textMuted,
    marginBottom: space.sm,
  },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    paddingHorizontal: space.md,
    paddingVertical: 5,
    borderRadius: radius.pill,
    gap: 6,
  },
  pillGlyph: { ...type.label },
  pillText: { ...type.label },
  primaryButton: {
    height: touch.primaryHeight,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: space.xl,
  },
  primaryButtonLabel: {
    ...type.subheading,
    color: palette.brandOn,
  },
  secondaryButton: {
    minHeight: touch.minTarget,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: semantic.borderStrong,
    backgroundColor: semantic.surface,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: space.xl,
  },
  secondaryButtonLabel: {
    ...type.bodyStrong,
    color: semantic.text,
  },
  pressed: { opacity: 0.7 },
  disabled: { opacity: 0.45 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    minHeight: touch.minTarget,
    paddingVertical: space.md,
    gap: space.md,
  },
  rowTitle: { ...type.bodyStrong, color: semantic.text },
  rowSubtitle: { ...type.caption, color: semantic.textSecondary, marginTop: 2 },
  skeleton: {
    backgroundColor: palette.grey200,
    borderRadius: radius.sm,
  },
  state: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: space.xxxl,
    paddingHorizontal: space.xl,
    gap: space.sm,
  },
  stateTitle: { ...type.subheading, color: semantic.text, textAlign: 'center' },
  stateBody: { ...type.body, color: semantic.textSecondary, textAlign: 'center' },
  offline: {
    backgroundColor: palette.warningSurface,
    paddingVertical: space.md,
    paddingHorizontal: space.lg,
  },
  offlineText: { ...type.label, color: palette.warningText, textAlign: 'center' },
});
