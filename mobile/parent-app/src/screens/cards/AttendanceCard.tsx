import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { AttendanceStatus, MonthAttendance } from '../../api/types';
import { useI18n } from '../../i18n';
import { TranslationKey } from '../../i18n/translations';

const STATUS_KEYS: Record<AttendanceStatus, TranslationKey> = {
  1: 'present',
  2: 'absent',
  3: 'late',
  4: 'halfDay',
  5: 'leave',
};

/** This month's attendance: percentage headline + recent day list. */
export default function AttendanceCard({ attendance }: { attendance: MonthAttendance | null }) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('attendanceTitle')}</Text>
      {!attendance || attendance.markedDays === 0 ? (
        <Text style={styles.muted}>{t('attendanceEmpty')}</Text>
      ) : (
        <>
          <View style={styles.row}>
            <Text style={styles.percent}>{attendance.attendancePercent}%</Text>
            <View style={styles.counters}>
              <Text style={styles.counter}>✅ {t('present')} {attendance.presentCount}</Text>
              <Text style={styles.counter}>❌ {t('absent')} {attendance.absentCount}</Text>
              <Text style={styles.counter}>⏰ {t('late')} {attendance.lateCount}</Text>
            </View>
          </View>
          {attendance.days
            .slice(-5)
            .reverse()
            .map((day) => (
              <View key={day.date} style={styles.dayRow}>
                <Text style={styles.dayDate}>
                  {new Date(day.date).toLocaleDateString('en-IN', {
                    day: '2-digit',
                    month: 'short',
                  })}
                </Text>
                <Text style={styles.dayStatus}>{t(STATUS_KEYS[day.status])}</Text>
                {day.remarks ? <Text style={styles.remarks}>{day.remarks}</Text> : null}
              </View>
            ))}
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
  row: { flexDirection: 'row', alignItems: 'center', marginBottom: 12 },
  percent: { fontSize: 36, fontWeight: '800', color: '#2E7D32', marginRight: 16 },
  counters: { gap: 2 },
  counter: { fontSize: 13, color: '#445' },
  dayRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 6,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
    gap: 12,
  },
  dayDate: { width: 60, color: '#667', fontSize: 13 },
  dayStatus: { fontWeight: '600', fontSize: 13 },
  remarks: { color: '#667', fontSize: 12, flexShrink: 1 },
});
