import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { ATTENDANCE_LABELS, MonthAttendance } from '../../api/types';

/** This month's attendance: percentage headline + recent day list. */
export default function AttendanceCard({ attendance }: { attendance: MonthAttendance | null }) {
  return (
    <View style={styles.card}>
      <Text style={styles.title}>Attendance this month</Text>
      {!attendance || attendance.markedDays === 0 ? (
        <Text style={styles.muted}>No attendance marked yet this month.</Text>
      ) : (
        <>
          <View style={styles.row}>
            <Text style={styles.percent}>{attendance.attendancePercent}%</Text>
            <View style={styles.counters}>
              <Text style={styles.counter}>✅ Present {attendance.presentCount}</Text>
              <Text style={styles.counter}>❌ Absent {attendance.absentCount}</Text>
              <Text style={styles.counter}>⏰ Late {attendance.lateCount}</Text>
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
                <Text style={styles.dayStatus}>{ATTENDANCE_LABELS[day.status]}</Text>
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
