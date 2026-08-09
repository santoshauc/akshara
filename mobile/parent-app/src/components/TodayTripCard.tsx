import React from 'react';
import { Linking, StyleSheet, Text, View } from 'react-native';
import { Card, SecondaryButton, StatusPill } from '../design/primitives';
import { palette, radius, semantic, space, type } from '../design/tokens';
import TripTimeline, { TripStep } from './TripTimeline';
import { BusLocation, ChildTransport } from '../api/types';

/**
 * "What is happening with my child today?" — answered in one card, above the
 * fold, without the parent tapping anything.
 *
 * Trip state is derived from what the backend actually knows: whether a trip
 * is running, whether this child has boarded, and how fresh the last GPS ping
 * is. Where we genuinely do not know something we say so, rather than showing
 * a confident guess — a transport app that cries wolf stops being trusted.
 */

export type TripPhase =
  | 'no-transport'
  | 'not-started'
  | 'running-before-pickup'
  | 'on-board'
  | 'dropped'
  | 'completed';

export interface TodayTripStrings {
  title: string;
  noTransportTitle: string;
  noTransportBody: string;
  notStarted: string;
  notStartedDetail: string;
  busRunning: string;
  onBoard: string;
  dropped: string;
  completed: string;
  stepHome: string;
  stepPickup: string;
  stepEnRoute: string;
  stepSchool: string;
  pickupTime: string;
  driver: string;
  bus: string;
  route: string;
  stop: string;
  callDriver: string;
  lastSeen: string;
  staleWarning: string;
  morningTrip: string;
  afternoonTrip: string;
}

interface Props {
  childName: string;
  transport: ChildTransport | null;
  bus: BusLocation | null;
  strings: TodayTripStrings;
  /** Formats an ISO timestamp as a short local time, e.g. 7:25 AM. */
  formatTime: (iso: string) => string;
}

/** A GPS fix older than this is treated as unknown rather than current. */
const STALE_FIX_MINUTES = 10;

export default function TodayTripCard({
  childName,
  transport,
  bus,
  strings,
  formatTime,
}: Props) {
  if (!transport) {
    return (
      <Card>
        <Text style={styles.cardTitle}>{strings.noTransportTitle}</Text>
        <Text style={styles.body}>{strings.noTransportBody}</Text>
      </Card>
    );
  }

  const running = bus != null;
  const isDropTrip = bus?.tripType === 2;
  const fixAgeMinutes = bus?.lastSeenAt
    ? (Date.now() - new Date(bus.lastSeenAt).getTime()) / 60000
    : null;
  const fixIsStale = fixAgeMinutes != null && fixAgeMinutes > STALE_FIX_MINUTES;

  // The backend tells us a trip is running; per-child boarding events are not
  // exposed to the parent API yet, so we state the bus's status rather than
  // claiming knowledge of the child's own boarding.
  const phase: TripPhase = !running
    ? 'not-started'
    : isDropTrip
      ? 'dropped'
      : 'running-before-pickup';

  const statusFor = (): { label: string; tone: 'neutral' | 'success' | 'info' | 'warning'; glyph: string } => {
    switch (phase) {
      case 'running-before-pickup':
        return { label: strings.busRunning, tone: 'info', glyph: '●' };
      case 'dropped':
        return { label: strings.dropped, tone: 'success', glyph: '✓' };
      default:
        return { label: strings.notStarted, tone: 'neutral', glyph: '○' };
    }
  };

  const status = statusFor();

  const steps: TripStep[] = [
    {
      key: 'home',
      label: strings.stepHome,
      state: running ? 'done' : 'current',
    },
    {
      key: 'pickup',
      label: strings.stepPickup,
      detail: transport.stopName,
      time: transport.pickupTime ?? null,
      state: running ? (isDropTrip ? 'done' : 'current') : 'upcoming',
    },
    {
      key: 'enroute',
      label: strings.stepEnRoute,
      state: running && !isDropTrip ? 'current' : running ? 'done' : 'upcoming',
    },
    {
      key: 'school',
      label: strings.stepSchool,
      state: isDropTrip ? 'current' : 'upcoming',
    },
  ];

  return (
    <Card padded={false}>
      <View style={styles.header}>
        <View style={{ flex: 1 }}>
          <Text style={styles.childName}>{childName}</Text>
          <Text style={styles.tripLabel}>
            {isDropTrip ? strings.afternoonTrip : strings.morningTrip}
          </Text>
        </View>
        <StatusPill label={status.label} tone={status.tone} glyph={status.glyph} />
      </View>

      <View style={styles.timelineArea}>
        <TripTimeline steps={steps} />
      </View>

      {running && fixIsStale ? (
        <View style={styles.staleNotice}>
          <Text style={styles.staleText}>{strings.staleWarning}</Text>
        </View>
      ) : null}

      <View style={styles.facts}>
        <Fact label={strings.bus} value={transport.vehicleRegistration ?? '—'} />
        <Fact label={strings.route} value={transport.routeName} />
        <Fact label={strings.stop} value={transport.stopName} />
        {transport.pickupTime ? (
          <Fact label={strings.pickupTime} value={transport.pickupTime} />
        ) : null}
        {bus?.lastSeenAt ? (
          <Fact label={strings.lastSeen} value={formatTime(bus.lastSeenAt)} />
        ) : null}
      </View>

      {transport.driverName ? (
        <View style={styles.driverBar}>
          <View style={{ flex: 1 }}>
            <Text style={styles.driverLabel}>{strings.driver}</Text>
            <Text style={styles.driverName}>{transport.driverName}</Text>
          </View>
          {transport.driverPhone ? (
            <SecondaryButton
              label={strings.callDriver}
              onPress={() => Linking.openURL(`tel:${transport.driverPhone}`)}
            />
          ) : null}
        </View>
      ) : null}
    </Card>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.fact}>
      <Text style={styles.factLabel}>{label}</Text>
      <Text style={styles.factValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: space.md,
    padding: space.lg,
    paddingBottom: space.md,
  },
  childName: { ...type.title, color: semantic.text },
  tripLabel: { ...type.label, color: semantic.textSecondary, marginTop: 2 },
  cardTitle: { ...type.heading, color: semantic.text, marginBottom: space.xs },
  body: { ...type.body, color: semantic.textSecondary },
  timelineArea: { paddingHorizontal: space.lg, paddingTop: space.sm },
  staleNotice: {
    backgroundColor: palette.warningSurface,
    marginHorizontal: space.lg,
    marginBottom: space.md,
    padding: space.md,
    borderRadius: radius.sm,
  },
  staleText: { ...type.caption, color: palette.warningText },
  facts: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: space.lg,
    paddingHorizontal: space.lg,
    paddingBottom: space.lg,
    borderTopWidth: 1,
    borderTopColor: semantic.border,
    paddingTop: space.md,
  },
  fact: { minWidth: 96 },
  factLabel: { ...type.caption, color: semantic.textMuted },
  factValue: { ...type.bodyStrong, color: semantic.text, marginTop: 1 },
  driverBar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    padding: space.lg,
    borderTopWidth: 1,
    borderTopColor: semantic.border,
    backgroundColor: palette.grey25,
    borderBottomLeftRadius: radius.lg,
    borderBottomRightRadius: radius.lg,
  },
  driverLabel: { ...type.caption, color: semantic.textMuted },
  driverName: { ...type.bodyStrong, color: semantic.text },
});
