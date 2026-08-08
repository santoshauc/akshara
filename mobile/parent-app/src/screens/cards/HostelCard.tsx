import React from 'react';
import { Linking, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { ChildHostel } from '../../api/types';
import { useI18n } from '../../i18n';

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });

/** The child's hostel stay with the warden contact. Hidden for day scholars. */
export default function HostelCard({ hostel }: { hostel: ChildHostel }) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('hostelTitle')}</Text>
      <View style={styles.row}>
        <Text style={styles.emoji}>🛏️</Text>
        <View style={styles.info}>
          <Text style={styles.name}>{hostel.hostelName}</Text>
          <Text style={styles.detail}>
            {t('hostelRoom', { room: hostel.roomNumber })} ·{' '}
            {t('hostelSince', { date: formatDate(hostel.allocatedOn) })}
          </Text>
          {hostel.wardenName && (
            <Text style={styles.detail}>
              {t('warden')}: {hostel.wardenName}
            </Text>
          )}
        </View>
      </View>
      {hostel.wardenPhone && (
        <TouchableOpacity
          style={styles.callButton}
          onPress={() => Linking.openURL(`tel:${hostel.wardenPhone}`)}
        >
          <Text style={styles.callText}>
            📞 {t('callWarden', { phone: hostel.wardenPhone })}
          </Text>
        </TouchableOpacity>
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
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  emoji: { fontSize: 28 },
  info: { flex: 1 },
  name: { fontSize: 15, fontWeight: '600' },
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
