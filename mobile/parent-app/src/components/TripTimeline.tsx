import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { palette, radius, semantic, space, type } from '../design/tokens';

/**
 * Where the child is in today's journey, as a vertical timeline.
 *
 * This is the parent app's single most important component: it has to answer
 * "where is my child?" in about two seconds, from across a room, without
 * being read word by word.
 *
 * Each step carries a glyph, a label and a state — never colour alone.
 * Completed steps show their timestamp, because "picked up" without a time
 * is only half an answer.
 */

export type StepState = 'done' | 'current' | 'upcoming' | 'exception';

export interface TripStep {
  key: string;
  label: string;
  /** Time it happened (done) or is expected (upcoming). */
  time?: string | null;
  /** Extra context, e.g. the stop name or a delay reason. */
  detail?: string | null;
  state: StepState;
}

const glyphFor: Record<StepState, string> = {
  done: '✓',
  current: '●',
  upcoming: '○',
  exception: '!',
};

const colourFor: Record<StepState, { dot: string; text: string; ring: string }> = {
  done: { dot: palette.success, text: semantic.textSecondary, ring: palette.successSurface },
  current: { dot: palette.brand, text: semantic.text, ring: palette.brandSurface },
  upcoming: { dot: palette.grey300, text: semantic.textMuted, ring: 'transparent' },
  exception: { dot: palette.danger, text: palette.dangerText, ring: palette.dangerSurface },
};

export default function TripTimeline({ steps }: { steps: TripStep[] }) {
  return (
    <View accessibilityRole="list">
      {steps.map((step, index) => {
        const colours = colourFor[step.state];
        const isLast = index === steps.length - 1;
        // The connector takes the colour of the step above it, so the line
        // "fills in" as the journey progresses.
        const connectorColour =
          step.state === 'done' ? palette.success : palette.grey200;

        return (
          <View
            key={step.key}
            style={styles.step}
            accessibilityRole="text"
            accessibilityLabel={[
              step.label,
              step.state === 'done' ? 'completed' : step.state === 'current' ? 'in progress' : '',
              step.time ?? '',
              step.detail ?? '',
            ]
              .filter(Boolean)
              .join('. ')}
          >
            <View style={styles.rail}>
              <View
                style={[
                  styles.dot,
                  {
                    backgroundColor: colours.dot,
                    borderColor: colours.ring,
                    // The current step is visibly larger, so the eye lands on
                    // "where the child is now" before reading anything.
                    transform: [{ scale: step.state === 'current' ? 1.18 : 1 }],
                  },
                ]}
              >
                <Text style={styles.dotGlyph} accessible={false}>
                  {glyphFor[step.state]}
                </Text>
              </View>
              {!isLast ? (
                <View style={[styles.connector, { backgroundColor: connectorColour }]} />
              ) : null}
            </View>

            <View style={styles.body}>
              <View style={styles.headline}>
                <Text
                  style={[
                    styles.label,
                    { color: colours.text },
                    step.state === 'current' && styles.labelCurrent,
                  ]}
                >
                  {step.label}
                </Text>
                {step.time ? <Text style={styles.time}>{step.time}</Text> : null}
              </View>
              {step.detail ? <Text style={styles.detail}>{step.detail}</Text> : null}
            </View>
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  step: { flexDirection: 'row', gap: space.md },
  rail: { alignItems: 'center', width: 24 },
  dot: {
    width: 22,
    height: 22,
    borderRadius: radius.pill,
    borderWidth: 3,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dotGlyph: { color: palette.white, fontSize: 11, fontWeight: '700', lineHeight: 13 },
  connector: { width: 2, flex: 1, minHeight: 18, marginVertical: 2 },
  body: { flex: 1, paddingBottom: space.lg },
  headline: { flexDirection: 'row', alignItems: 'baseline', gap: space.sm },
  label: { ...type.body, flex: 1 },
  labelCurrent: { ...type.bodyStrong },
  time: { ...type.label, color: semantic.textSecondary },
  detail: { ...type.caption, color: semantic.textMuted, marginTop: 2 },
});
