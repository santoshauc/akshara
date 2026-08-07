import React, { useState } from 'react';
import { ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { TimetableEntry } from '../../api/types';
import { BRAND } from '../../config';
import { useI18n } from '../../i18n';
import { TranslationKey } from '../../i18n/translations';

const DAY_KEYS: TranslationKey[] = [
  'dayMon', 'dayMon', 'dayTue', 'dayWed', 'dayThu', 'dayFri', 'daySat', 'daySun',
];
const DAY_SHORT_KEYS: TranslationKey[] = [
  'dayMonShort', 'dayMonShort', 'dayTueShort', 'dayWedShort', 'dayThuShort',
  'dayFriShort', 'daySatShort', 'daySunShort',
];

const formatTime = (value: string) => {
  const [hours, minutes] = value.split(':').map(Number);
  const suffix = hours >= 12 ? 'PM' : 'AM';
  const displayHours = hours % 12 === 0 ? 12 : hours % 12;
  return `${displayHours}:${String(minutes).padStart(2, '0')} ${suffix}`;
};

/** JS getDay(): Sunday=0 → ISO 1–7. */
const todayIso = () => {
  const day = new Date().getDay();
  return day === 0 ? 7 : day;
};

/**
 * Calendar view of the published schedule: day tabs (defaulting to today)
 * with the selected day's periods as a time-ordered agenda.
 */
export default function TimetableCard({ entries }: { entries: TimetableEntry[] }) {
  const { t } = useI18n();
  const days = [...new Set(entries.map((e) => e.dayOfWeek))].sort((a, b) => a - b);
  const [selectedDay, setSelectedDay] = useState<number>(() =>
    days.includes(todayIso()) ? todayIso() : (days[0] ?? 1),
  );

  const activeDay = days.includes(selectedDay) ? selectedDay : (days[0] ?? 1);
  const slots = entries
    .filter((e) => e.dayOfWeek === activeDay)
    .sort((a, b) => a.period - b.period);

  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('timetableTitle')}</Text>
      {entries.length === 0 ? (
        <Text style={styles.muted}>{t('timetableEmpty')}</Text>
      ) : (
        <>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.tabs}>
            {days.map((day) => {
              const active = day === activeDay;
              return (
                <TouchableOpacity
                  key={day}
                  style={[styles.tab, active && styles.tabActive]}
                  onPress={() => setSelectedDay(day)}
                >
                  <Text style={[styles.tabText, active && styles.tabTextActive]}>
                    {t(DAY_SHORT_KEYS[day])}
                    {day === todayIso() ? ' •' : ''}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </ScrollView>

          <Text style={styles.dayHeading}>
            {t(DAY_KEYS[activeDay])}
            {activeDay === todayIso() ? ` (${t('today')})` : ''}
          </Text>

          {slots.map((entry) => (
            <View key={entry.id} style={styles.slot}>
              <View style={styles.timeline}>
                <View style={styles.dot} />
                <View style={styles.line} />
              </View>
              <View style={styles.slotBody}>
                <Text style={styles.time}>
                  {formatTime(entry.startTime)} – {formatTime(entry.endTime)}
                </Text>
                <Text style={styles.subject}>{entry.subjectName}</Text>
                {entry.teacherName ? (
                  <Text style={styles.teacher}>{entry.teacherName}</Text>
                ) : null}
              </View>
              <Text style={styles.period}>P{entry.period}</Text>
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
  tabs: { marginBottom: 10 },
  tab: {
    paddingVertical: 6,
    paddingHorizontal: 14,
    borderRadius: 16,
    backgroundColor: '#F0F2F6',
    marginRight: 6,
  },
  tabActive: { backgroundColor: BRAND },
  tabText: { fontSize: 13, fontWeight: '600', color: '#445' },
  tabTextActive: { color: '#fff' },
  dayHeading: { fontSize: 13, fontWeight: '700', color: BRAND, marginBottom: 6 },
  slot: { flexDirection: 'row', alignItems: 'stretch' },
  timeline: { width: 18, alignItems: 'center' },
  dot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: BRAND,
    marginTop: 6,
  },
  line: { flex: 1, width: 2, backgroundColor: '#E1E5EC' },
  slotBody: { flex: 1, paddingBottom: 12 },
  time: { fontSize: 12, color: '#667' },
  subject: { fontSize: 15, fontWeight: '600', marginTop: 1 },
  teacher: { fontSize: 12, color: '#667', marginTop: 1 },
  period: { fontSize: 11, color: '#99A', fontWeight: '700', marginTop: 6 },
});
