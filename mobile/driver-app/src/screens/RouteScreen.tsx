import * as Location from 'expo-location';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { driverApi, logout } from '../api/driver';
import { DriverRoute, ManifestRider, RiderEventType, TripType } from '../api/types';
import { BRAND, PING_INTERVAL_SECONDS } from '../config';

interface Props {
  onSignedOut: () => void;
}

const CHECKLIST = ['Fuel level', 'Tyres', 'Brakes', 'Emergency kit'] as const;

/**
 * The driver's single working screen: manifest + inspection-gated trip start,
 * then per-rider board/drop marking with a background GPS ping loop.
 */
export default function RouteScreen({ onSignedOut }: Props) {
  const [route, setRoute] = useState<DriverRoute | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [checks, setChecks] = useState<boolean[]>(CHECKLIST.map(() => false));
  const [tripType, setTripType] = useState<TripType>(1);
  const [marked, setMarked] = useState<Record<string, RiderEventType>>({});
  const pingTimer = useRef<ReturnType<typeof setInterval> | null>(null);

  const load = useCallback(async () => {
    try {
      const data = await driverApi.getRoute();
      setRoute(data);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load your route.');
      setRoute(null);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  /** GPS ping loop while a trip is active. Failures are silent — the bus keeps moving. */
  useEffect(() => {
    const stop = () => {
      if (pingTimer.current) {
        clearInterval(pingTimer.current);
        pingTimer.current = null;
      }
    };

    if (!route?.activeTripId) {
      stop();
      return stop;
    }

    const ping = async () => {
      try {
        const { status } = await Location.requestForegroundPermissionsAsync();
        if (status !== 'granted') {
          return;
        }
        const position = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced,
        });
        await driverApi.recordLocation(position.coords.latitude, position.coords.longitude);
      } catch {
        // Transient GPS/network failures must never crash the trip screen.
      }
    };

    void ping();
    pingTimer.current = setInterval(() => void ping(), PING_INTERVAL_SECONDS * 1000);
    return stop;
  }, [route?.activeTripId]);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  const startTrip = async () => {
    if (!checks.every(Boolean)) {
      setError('Complete every inspection item before starting the trip.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await driverApi.startTrip(tripType, true, null);
      setMarked({});
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not start the trip.');
    } finally {
      setBusy(false);
    }
  };

  const mark = async (rider: ManifestRider, eventType: RiderEventType) => {
    try {
      await driverApi.markRider(rider.studentId, eventType, null);
      setMarked((current) => ({ ...current, [rider.studentId]: eventType }));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not record the event.');
    }
  };

  const endTrip = async () => {
    setBusy(true);
    try {
      await driverApi.endTrip();
      setChecks(CHECKLIST.map(() => false));
      setMarked({});
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not end the trip.');
    } finally {
      setBusy(false);
    }
  };

  const signOut = async () => {
    await logout();
    onSignedOut();
  };

  if (!route && !error) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={BRAND} />
      </View>
    );
  }

  const active = route?.activeTripId != null;
  const isPickup = (route?.activeTripType ?? tripType) === 1;

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
    >
      <View style={styles.header}>
        <View>
          <Text style={styles.headerTitle}>{route?.routeName ?? 'My route'}</Text>
          {route?.vehicleRegistration && (
            <Text style={styles.headerMeta}>🚌 {route.vehicleRegistration}</Text>
          )}
        </View>
        <TouchableOpacity onPress={signOut}>
          <Text style={styles.signOut}>Sign out</Text>
        </TouchableOpacity>
      </View>

      {error && <Text style={styles.error}>{error}</Text>}

      {route && !active && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Pre-trip inspection</Text>
          {CHECKLIST.map((item, index) => (
            <TouchableOpacity
              key={item}
              style={styles.checkRow}
              onPress={() =>
                setChecks((current) => current.map((c, i) => (i === index ? !c : c)))
              }
            >
              <Text style={styles.checkbox}>{checks[index] ? '☑' : '☐'}</Text>
              <Text style={styles.checkLabel}>{item}</Text>
            </TouchableOpacity>
          ))}

          <View style={styles.typeRow}>
            {([1, 2] as TripType[]).map((type) => (
              <TouchableOpacity
                key={type}
                style={[styles.typeChip, tripType === type && styles.typeChipActive]}
                onPress={() => setTripType(type)}
              >
                <Text style={[styles.typeText, tripType === type && styles.typeTextActive]}>
                  {type === 1 ? 'Morning pickup' : 'Evening drop'}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <TouchableOpacity
            style={[styles.button, !checks.every(Boolean) && styles.buttonDisabled]}
            onPress={startTrip}
            disabled={busy || !checks.every(Boolean)}
          >
            {busy ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={styles.buttonText}>Start trip</Text>
            )}
          </TouchableOpacity>
        </View>
      )}

      {route && active && (
        <>
          <View style={styles.activeBanner}>
            <Text style={styles.activeText}>
              {isPickup ? '🌅 Pickup trip in progress' : '🌇 Drop trip in progress'} · GPS on
            </Text>
          </View>

          {route.stops.map((stop) => {
            const riders = route.riders.filter((r) => r.stopOrder === stop.sortOrder);
            if (riders.length === 0) {
              return null;
            }
            return (
              <View key={stop.id} style={styles.card}>
                <Text style={styles.cardTitle}>
                  {stop.sortOrder}. {stop.name}
                </Text>
                {riders.map((rider) => {
                  const state = marked[rider.studentId];
                  return (
                    <View key={rider.studentId} style={styles.riderRow}>
                      <View style={styles.riderInfo}>
                        <Text style={styles.riderName}>{rider.studentName}</Text>
                        <Text style={styles.riderMeta}>{rider.className ?? ''}</Text>
                      </View>
                      {state ? (
                        <Text style={styles.riderState}>
                          {state === 3 ? '❌ Absent' : isPickup ? '✅ On board' : '✅ Dropped'}
                        </Text>
                      ) : (
                        <View style={styles.riderActions}>
                          <TouchableOpacity
                            style={styles.actionButton}
                            onPress={() => void mark(rider, isPickup ? 1 : 2)}
                          >
                            <Text style={styles.actionText}>
                              {isPickup ? 'Board' : 'Drop'}
                            </Text>
                          </TouchableOpacity>
                          <TouchableOpacity
                            style={[styles.actionButton, styles.absentButton]}
                            onPress={() => void mark(rider, 3)}
                          >
                            <Text style={styles.absentText}>Absent</Text>
                          </TouchableOpacity>
                        </View>
                      )}
                    </View>
                  );
                })}
              </View>
            );
          })}

          <TouchableOpacity style={[styles.button, styles.endButton]} onPress={endTrip} disabled={busy}>
            {busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>End trip</Text>}
          </TouchableOpacity>
        </>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#FFF7F0' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    paddingTop: 60,
  },
  headerTitle: { fontSize: 22, fontWeight: '700' },
  headerMeta: { fontSize: 13, color: '#775', marginTop: 2 },
  signOut: { color: BRAND, fontWeight: '600' },
  error: { color: '#C62828', textAlign: 'center', marginHorizontal: 24, marginBottom: 8 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 14,
    padding: 18,
    marginHorizontal: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  cardTitle: { fontSize: 16, fontWeight: '700', marginBottom: 10 },
  checkRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: 8 },
  checkbox: { fontSize: 20, width: 32, color: BRAND },
  checkLabel: { fontSize: 15 },
  typeRow: { flexDirection: 'row', gap: 8, marginTop: 12, marginBottom: 4 },
  typeChip: {
    borderRadius: 16,
    paddingVertical: 8,
    paddingHorizontal: 14,
    backgroundColor: '#F4E8DC',
  },
  typeChipActive: { backgroundColor: BRAND },
  typeText: { fontSize: 13, fontWeight: '600', color: '#775' },
  typeTextActive: { color: '#fff' },
  button: {
    backgroundColor: BRAND,
    borderRadius: 10,
    padding: 15,
    alignItems: 'center',
    marginTop: 12,
    marginHorizontal: 16,
    marginBottom: 24,
  },
  buttonDisabled: { opacity: 0.4 },
  endButton: { backgroundColor: '#455A64' },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
  activeBanner: {
    backgroundColor: '#E8F5E9',
    borderRadius: 10,
    padding: 12,
    marginHorizontal: 16,
    marginBottom: 12,
  },
  activeText: { color: '#2E7D32', fontWeight: '600', textAlign: 'center' },
  riderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    borderTopWidth: 1,
    borderTopColor: '#F5EDE4',
  },
  riderInfo: { flex: 1 },
  riderName: { fontSize: 15, fontWeight: '600' },
  riderMeta: { fontSize: 12, color: '#775' },
  riderState: { fontSize: 14, fontWeight: '600', color: '#2E7D32' },
  riderActions: { flexDirection: 'row', gap: 8 },
  actionButton: {
    backgroundColor: BRAND,
    borderRadius: 8,
    paddingVertical: 8,
    paddingHorizontal: 14,
  },
  actionText: { color: '#fff', fontWeight: '600', fontSize: 13 },
  absentButton: { backgroundColor: '#F4E8DC' },
  absentText: { color: '#A55', fontWeight: '600', fontSize: 13 },
});
