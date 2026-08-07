import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { TimetableEntry } from '../../api/types';

const DAY_NAMES = ['', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

const formatTime = (value: string) => {
  const [hours, minutes] = value.split(':').map(Number);
  const suffix = hours >= 12 ? 'PM' : 'AM';
  const displayHours = hours % 12 === 0 ? 12 : hours % 12;
  return `${displayHours}:${String(minutes).padStart(2, '0')} ${suffix}`;
};

/** The child's published weekly schedule, grouped by day. */
export default function TimetableCard({ entries }: { entries: TimetableEntry[] }) {
  const days = [...new Set(entries.map((e) => e.dayOfWeek))].sort((a, b) => a - b);

  return (
    <View style={styles.card}>
      <Text style={styles.title}>Class timetable</Text>
      {entries.length === 0 ? (
        <Text style={styles.muted}>The school hasn't published a timetable yet.</Text>
      ) : (
        days.map((day) => (
          <View key={day} style={styles.day}>
            <Text style={styles.dayName}>{DAY_NAMES[day]}</Text>
            {entries
              .filter((e) => e.dayOfWeek === day)
              .map((entry) => (
                <View key={entry.id} style={styles.slot}>
                  <Text style={styles.time}>
                    {formatTime(entry.startTime)}–{formatTime(entry.endTime)}
                  </Text>
                  <Text style={styles.subject}>{entry.subjectName}</Text>
                  {entry.teacherName ? (
                    <Text style={styles.teacher}>{entry.teacherName}</Text>
                  ) : null}
                </View>
              ))}
          </View>
        ))
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
  day: { marginBottom: 10 },
  dayName: { fontSize: 13, fontWeight: '700', color: '#1565C0', marginBottom: 4 },
  slot: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 5,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
    gap: 10,
  },
  time: { width: 130, fontSize: 12, color: '#667' },
  subject: { flex: 1, fontSize: 14, fontWeight: '600' },
  teacher: { fontSize: 12, color: '#667' },
});
