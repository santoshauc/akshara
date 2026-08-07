import React, { useEffect, useState } from 'react';
import { Linking, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { parentApi } from '../../api/parent';
import { BusLocation } from '../../api/types';
import { useI18n } from '../../i18n';

const POLL_SECONDS = 20;

const formatClock = (iso: string) => {
  const date = new Date(iso);
  const hours = date.getHours();
  const suffix = hours >= 12 ? 'PM' : 'AM';
  const displayHours = hours % 12 === 0 ? 12 : hours % 12;
  return `${displayHours}:${String(date.getMinutes()).padStart(2, '0')} ${suffix}`;
};

/**
 * Live bus tracking during an active trip on the child's route.
 * Polls the parent bus endpoint while mounted; idle state when no trip runs.
 */
export default function BusLiveCard({ studentId }: { studentId: string }) {
  const { t } = useI18n();
  const [bus, setBus] = useState<BusLocation | null>(null);
  const [loaded, setLoaded] = useState(false);

  const formatAgo = (iso: string) => {
    const seconds = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
    if (seconds < 60) {
      return t('justNow');
    }
    const minutes = Math.round(seconds / 60);
    return minutes === 1 ? t('minuteAgo') : t('minutesAgo', { count: minutes });
  };

  useEffect(() => {
    let cancelled = false;

    const poll = async () => {
      try {
        const data = await parentApi.getBus(studentId);
        if (!cancelled) {
          setBus(data);
          setLoaded(true);
        }
      } catch {
        // Keep the last known state on transient errors.
      }
    };

    setLoaded(false);
    setBus(null);
    void poll();
    const timer = setInterval(() => void poll(), POLL_SECONDS * 1000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [studentId]);

  const hasFix = bus?.latitude != null && bus?.longitude != null;

  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('liveBusTitle')}</Text>
      {!bus ? (
        <Text style={styles.muted}>
          {loaded ? t('liveBusIdle') : t('liveBusChecking')}
        </Text>
      ) : (
        <>
          <View style={styles.liveRow}>
            <View style={styles.liveDot} />
            <Text style={styles.liveText}>
              {bus.tripType === 1 ? t('pickupInProgress') : t('dropInProgress')} ·{' '}
              {t('started')} {formatClock(bus.startedAt)}
            </Text>
          </View>
          {hasFix ? (
            <>
              {bus.lastSeenAt && (
                <Text style={styles.detail}>
                  {t('locationUpdated', { when: formatAgo(bus.lastSeenAt) })}
                </Text>
              )}
              <TouchableOpacity
                style={styles.mapButton}
                onPress={() =>
                  Linking.openURL(
                    `https://www.google.com/maps?q=${bus.latitude},${bus.longitude}`,
                  )
                }
              >
                <Text style={styles.mapText}>{t('openInMaps')}</Text>
              </TouchableOpacity>
            </>
          ) : (
            <Text style={styles.detail}>{t('waitingForGps')}</Text>
          )}
        </>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: '#fff',
    borderRadius: 14,
    padding: 18,
    shadowColor: '#000',
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  title: { fontSize: 16, fontWeight: '700', marginBottom: 10 },
  muted: { color: '#667' },
  liveRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  liveDot: { width: 10, height: 10, borderRadius: 5, backgroundColor: '#2E7D32' },
  liveText: { color: '#2E7D32', fontWeight: '600', flex: 1 },
  detail: { fontSize: 13, color: '#445', marginTop: 8 },
  mapButton: {
    marginTop: 12,
    backgroundColor: '#E8F5E9',
    borderRadius: 10,
    padding: 12,
    alignItems: 'center',
  },
  mapText: { color: '#2E7D32', fontWeight: '600', fontSize: 14 },
});
