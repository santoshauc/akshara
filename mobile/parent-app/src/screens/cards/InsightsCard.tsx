import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { StudentInsights } from '../../api/types';
import { BRAND } from '../../config';
import { useI18n } from '../../i18n';

/** Two proportional bars: the child beside the class average. */
function CompareBars({
  label,
  childValue,
  classValue,
  childLabel,
  classLabel,
}: {
  label: string;
  childValue: number;
  classValue: number;
  childLabel: string;
  classLabel: string;
}) {
  return (
    <View style={styles.compareBlock}>
      <Text style={styles.subject}>{label}</Text>
      <View style={styles.barRow}>
        <View style={[styles.bar, styles.childBar, { flex: Math.max(childValue, 2) }]} />
        <View style={{ flex: Math.max(100 - childValue, 0.01) }} />
        <Text style={styles.barValue}>
          {childLabel} {childValue.toFixed(0)}%
        </Text>
      </View>
      <View style={styles.barRow}>
        <View style={[styles.bar, styles.classBar, { flex: Math.max(classValue, 2) }]} />
        <View style={{ flex: Math.max(100 - classValue, 0.01) }} />
        <Text style={styles.barValue}>
          {classLabel} {classValue.toFixed(0)}%
        </Text>
      </View>
    </View>
  );
}

/**
 * How the child compares with their class: per-subject marks beside the class
 * average, rank, and this month's attendance — aggregates only, no other
 * child is ever named.
 */
export default function InsightsCard({ insights }: { insights: StudentInsights }) {
  const { t } = useI18n();
  const hasExam = insights.examName !== null && insights.subjects.length > 0;
  const hasAttendance =
    insights.childAttendancePercent !== null && insights.classAttendancePercent !== null;
  if (!hasExam && !hasAttendance) {
    return null;
  }

  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('insightsTitle')}</Text>

      {hasExam && (
        <>
          <Text style={styles.meta}>
            {insights.examName}
            {insights.rank !== null && insights.sectionSize !== null
              ? ` · ${t('insightsRank')} ${insights.rank}/${insights.sectionSize}`
              : ''}
          </Text>
          {insights.subjects.map((subject) => (
            <CompareBars
              key={subject.subject}
              label={subject.subject}
              childValue={subject.childPercent}
              classValue={subject.classAverage}
              childLabel={t('insightsChild')}
              classLabel={t('insightsClass')}
            />
          ))}
        </>
      )}

      {hasAttendance && (
        <CompareBars
          label={t('insightsAttendance')}
          childValue={insights.childAttendancePercent!}
          classValue={insights.classAttendancePercent!}
          childLabel={t('insightsChild')}
          classLabel={t('insightsClass')}
        />
      )}

      <Text style={styles.footnote}>{t('insightsFootnote')}</Text>
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
  title: { fontSize: 16, fontWeight: '700', marginBottom: 2 },
  meta: { fontSize: 12, color: '#667', marginBottom: 8 },
  compareBlock: { marginTop: 10 },
  subject: { fontSize: 13, fontWeight: '600', marginBottom: 4 },
  barRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 3 },
  bar: { height: 10, borderRadius: 5 },
  childBar: { backgroundColor: BRAND },
  classBar: { backgroundColor: '#C3CBD9' },
  barValue: { fontSize: 11, color: '#556', marginLeft: 6, minWidth: 86 },
  footnote: { fontSize: 11, color: '#889', marginTop: 12, lineHeight: 15 },
});
