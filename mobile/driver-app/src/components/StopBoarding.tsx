import React, { useMemo } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Card, StatusPill } from '../design/primitives';
import { palette, radius, semantic, space, touch, type } from '../design/tokens';
import { ManifestRider, RiderEventType, TripType } from '../api/types';

/**
 * Trip Mode: the driver's working surface once a trip has started.
 *
 * Designed for a person in a vehicle, often standing, sometimes in sun, with
 * one hand free. That drives every decision here:
 *
 *  - Students are grouped by stop and only the CURRENT stop is expanded, so
 *    the driver is never scanning a 24-name list for the four children in
 *    front of them.
 *  - Actions are full-height buttons, not checkboxes. A mis-tap on a child's
 *    attendance is a safeguarding problem, not a typo.
 *  - Each student shows a state with a word and a glyph, never colour alone.
 *  - Completed stops collapse to a one-line summary so progress is obvious
 *    without scrolling back.
 */

export interface StopBoardingStrings {
  currentStop: string;
  nextStop: string;
  waiting: string;
  pickedUp: string;
  dropped: string;
  absent: string;
  markPickedUp: string;
  markDropped: string;
  markAbsent: string;
  studentsAtStop: string;
  stopComplete: string;
  allStopsDone: string;
  ofCount: string;
}

interface Props {
  riders: ManifestRider[];
  marked: Record<string, RiderEventType>;
  tripType: TripType;
  strings: StopBoardingStrings;
  busy: boolean;
  onMark: (studentId: string, event: RiderEventType) => void;
}

interface StopGroup {
  name: string;
  order: number;
  riders: ManifestRider[];
  settled: number;
}

export default function StopBoarding({
  riders,
  marked,
  tripType,
  strings,
  busy,
  onMark,
}: Props) {
  const groups = useMemo<StopGroup[]>(() => {
    const byStop = new Map<string, StopGroup>();
    for (const rider of riders) {
      const existing = byStop.get(rider.stopName);
      if (existing) {
        existing.riders.push(rider);
      } else {
        byStop.set(rider.stopName, {
          name: rider.stopName,
          order: rider.stopOrder,
          riders: [rider],
          settled: 0,
        });
      }
    }

    const list = [...byStop.values()].sort((a, b) => a.order - b.order);
    for (const group of list) {
      group.settled = group.riders.filter((r) => marked[r.studentId] != null).length;
    }

    return list;
  }, [riders, marked]);

  // The first stop with anyone still unmarked is where the bus is now.
  const currentIndex = groups.findIndex((g) => g.settled < g.riders.length);
  const isPickup = tripType === 1;

  if (groups.length === 0) {
    return null;
  }

  if (currentIndex === -1) {
    return (
      <Card>
        <StatusPill label={strings.allStopsDone} tone="success" glyph="✓" />
      </Card>
    );
  }

  return (
    <View style={{ gap: space.md }}>
      {groups.map((group, index) => {
        const isCurrent = index === currentIndex;
        const isDone = group.settled === group.riders.length;

        if (!isCurrent) {
          // Collapsed: enough to know where we have been and what is coming.
          return (
            <View key={group.name} style={styles.collapsed}>
              <Text style={styles.collapsedName} numberOfLines={1}>
                {group.order}. {group.name}
              </Text>
              {isDone ? (
                <StatusPill label={strings.stopComplete} tone="success" glyph="✓" />
              ) : (
                <Text style={styles.collapsedCount}>
                  {group.riders.length} {strings.studentsAtStop}
                </Text>
              )}
            </View>
          );
        }

        return (
          <Card key={group.name} padded={false}>
            <View style={styles.currentHeader}>
              <View style={{ flex: 1 }}>
                <Text style={styles.currentLabel}>{strings.currentStop}</Text>
                <Text style={styles.currentName}>{group.name}</Text>
              </View>
              <Text style={styles.progress}>
                {group.settled} {strings.ofCount} {group.riders.length}
              </Text>
            </View>

            {group.riders.map((rider) => {
              const state = marked[rider.studentId];
              return (
                <View key={rider.studentId} style={styles.rider}>
                  <View style={styles.riderHead}>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.riderName}>{rider.studentName}</Text>
                      {rider.className ? (
                        <Text style={styles.riderClass}>{rider.className}</Text>
                      ) : null}
                    </View>
                    {state != null ? (
                      <StatusPill
                        label={
                          state === 3
                            ? strings.absent
                            : state === 1
                              ? strings.pickedUp
                              : strings.dropped
                        }
                        tone={state === 3 ? 'warning' : 'success'}
                        glyph={state === 3 ? '!' : '✓'}
                      />
                    ) : (
                      <StatusPill label={strings.waiting} tone="neutral" glyph="○" />
                    )}
                  </View>

                  {state == null ? (
                    <View style={styles.actions}>
                      <BigAction
                        label={isPickup ? strings.markPickedUp : strings.markDropped}
                        tone="confirm"
                        disabled={busy}
                        onPress={() => onMark(rider.studentId, isPickup ? 1 : 2)}
                        accessibilityLabel={`${
                          isPickup ? strings.markPickedUp : strings.markDropped
                        }: ${rider.studentName}`}
                      />
                      {/* Absent only makes sense on a pickup run — on the way
                          home the child is already aboard. */}
                      {isPickup ? (
                        <BigAction
                          label={strings.markAbsent}
                          tone="exception"
                          disabled={busy}
                          onPress={() => onMark(rider.studentId, 3)}
                          accessibilityLabel={`${strings.markAbsent}: ${rider.studentName}`}
                        />
                      ) : null}
                    </View>
                  ) : null}
                </View>
              );
            })}
          </Card>
        );
      })}
    </View>
  );
}

/** A deliberately large action — thumb-sized, unambiguous, hard to mis-hit. */
function BigAction({
  label,
  tone,
  onPress,
  disabled,
  accessibilityLabel,
}: {
  label: string;
  tone: 'confirm' | 'exception';
  onPress: () => void;
  disabled?: boolean;
  accessibilityLabel: string;
}) {
  const confirm = tone === 'confirm';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ disabled: !!disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.bigAction,
        confirm ? styles.confirmAction : styles.exceptionAction,
        pressed && { opacity: 0.75 },
        disabled && { opacity: 0.45 },
      ]}
    >
      <Text style={[styles.bigActionLabel, confirm && { color: palette.brandOn }]}>
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  collapsed: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    paddingVertical: space.md,
    paddingHorizontal: space.lg,
    backgroundColor: semantic.surfaceSunken,
    borderRadius: radius.md,
  },
  collapsedName: { ...type.body, color: semantic.textSecondary, flex: 1 },
  collapsedCount: { ...type.caption, color: semantic.textMuted },
  currentHeader: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: space.md,
    padding: space.lg,
    borderBottomWidth: 1,
    borderBottomColor: semantic.border,
  },
  currentLabel: { ...type.overline, color: palette.brand },
  currentName: { ...type.title, color: semantic.text, marginTop: 2 },
  progress: { ...type.bodyStrong, color: semantic.textSecondary },
  rider: {
    paddingHorizontal: space.lg,
    paddingVertical: space.md,
    borderBottomWidth: 1,
    borderBottomColor: semantic.border,
  },
  riderHead: { flexDirection: 'row', alignItems: 'center', gap: space.md },
  riderName: { ...type.subheading, color: semantic.text },
  riderClass: { ...type.caption, color: semantic.textMuted, marginTop: 1 },
  actions: { flexDirection: 'row', gap: space.md, marginTop: space.md },
  bigAction: {
    flex: 1,
    minHeight: touch.primaryHeight,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: space.md,
  },
  confirmAction: { backgroundColor: palette.brand },
  exceptionAction: {
    backgroundColor: palette.warningSurface,
    borderWidth: 1,
    borderColor: palette.warning,
  },
  bigActionLabel: { ...type.subheading, color: palette.warningText, textAlign: 'center' },
});
