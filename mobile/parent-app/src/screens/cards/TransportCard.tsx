import React from 'react';
import { Linking, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { ChildTransport } from '../../api/types';
import { useI18n } from '../../i18n';

const formatTime = (value: string) => {
  const [hours, minutes] = value.split(':').map(Number);
  const suffix = hours >= 12 ? 'PM' : 'AM';
  const displayHours = hours % 12 === 0 ? 12 : hours % 12;
  return `${displayHours}:${String(minutes).padStart(2, '0')} ${suffix}`;
};

/** The child's bus route, stop and driver contact. */
export default function TransportCard({ transport }: { transport: ChildTransport | null }) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('transportTitle')}</Text>
      {!transport ? (
        <Text style={styles.muted}>{t('transportEmpty')}</Text>
      ) : (
        <>
          <View style={styles.row}>
            <Text style={styles.emoji}>🚌</Text>
            <View style={styles.info}>
              <Text style={styles.route}>{transport.routeName}</Text>
              <Text style={styles.detail}>
                {t('stop')}: {transport.stopName}
                {transport.pickupTime ? ` · ${t('pickup')} ${formatTime(transport.pickupTime)}` : ''}
              </Text>
              {transport.vehicleRegistration && (
                <Text style={styles.detail}>{t('bus')} {transport.vehicleRegistration}</Text>
              )}
            </View>
          </View>
          {transport.driverPhone && (
            <TouchableOpacity
              style={styles.callButton}
              onPress={() => Linking.openURL(`tel:${transport.driverPhone}`)}
            >
              <Text style={styles.callText}>
                📞 {t('callDriver', {
                  name: transport.driverName ?? t('driver'),
                  phone: transport.driverPhone,
                })}
              </Text>
            </TouchableOpacity>
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
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  emoji: { fontSize: 28 },
  info: { flex: 1 },
  route: { fontSize: 15, fontWeight: '600' },
  detail: { fontSize: 13, color: '#445', marginTop: 1 },
  callButton: {
    marginTop: 12,
    backgroundColor: '#E8F0FB',
    borderRadius: 10,
    padding: 12,
    alignItems: 'center',
  },
  callText: { color: '#1565C0', fontWeight: '600', fontSize: 14 },
});
