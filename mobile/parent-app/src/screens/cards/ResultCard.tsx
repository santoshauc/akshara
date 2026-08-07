import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { StudentResult } from '../../api/types';
import { useI18n } from '../../i18n';

/** Latest published exam result as a mini report card. */
export default function ResultCard({
  result,
  examCount,
}: {
  result: StudentResult | null;
  examCount: number;
}) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('resultTitle')}</Text>
      {!result ? (
        <Text style={styles.muted}>
          {examCount === 0 ? t('resultNonePublished') : t('resultNoMarks')}
        </Text>
      ) : (
        <>
          <Text style={styles.examName}>{result.examName}</Text>
          {result.lines.map((line) => (
            <View key={line.subjectName} style={styles.line}>
              <Text style={styles.subject}>{line.subjectName}</Text>
              <Text style={styles.marks}>
                {line.isAbsent ? t('resultAbsent') : `${line.marksObtained} / ${line.maxMarks}`}
              </Text>
              <Text style={[styles.grade, !line.passed && styles.gradeFail]}>{line.grade}</Text>
            </View>
          ))}
          <View style={styles.summary}>
            <Text style={styles.summaryText}>
              {t('total')} {result.totalObtained}/{result.totalMax} · {result.percent}% ·{' '}
              {t('grade')} {result.overallGrade}
            </Text>
            {result.sectionRank != null && (
              <Text style={styles.rank}>
                {t('rankOf', { rank: result.sectionRank, size: result.sectionSize })}
              </Text>
            )}
          </View>
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
  examName: { fontSize: 14, fontWeight: '600', color: '#1565C0', marginBottom: 8 },
  line: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 6,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
  },
  subject: { flex: 1, fontSize: 14 },
  marks: { width: 90, textAlign: 'right', color: '#445', fontSize: 14 },
  grade: { width: 40, textAlign: 'right', fontWeight: '700', color: '#2E7D32' },
  gradeFail: { color: '#C62828' },
  summary: { marginTop: 10, gap: 2 },
  summaryText: { fontWeight: '700', fontSize: 14 },
  rank: { color: '#667', fontSize: 13 },
});
