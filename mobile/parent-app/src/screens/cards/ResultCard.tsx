import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import React, { useState } from 'react';
import { ActivityIndicator, Platform, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { parentApi } from '../../api/parent';
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
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const downloadReportCard = async () => {
    if (!result) {
      return;
    }
    setDownloading(true);
    setDownloadError(null);
    try {
      const bytes = await parentApi.getReportCard(result.studentId, result.examId);
      if (Platform.OS === 'web') {
        // Browser: open the PDF via a blob URL (viewer or download per settings).
        const blob = new Blob([bytes], { type: 'application/pdf' });
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      } else {
        // Device: write to cache and hand off to the system share sheet.
        const file = new File(Paths.cache, 'report-card.pdf');
        file.write(new Uint8Array(bytes));
        await Sharing.shareAsync(file.uri, { mimeType: 'application/pdf' });
      }
    } catch {
      setDownloadError(t('reportCardFailed'));
    } finally {
      setDownloading(false);
    }
  };
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
          {downloadError && <Text style={styles.downloadError}>{downloadError}</Text>}
          <TouchableOpacity
            style={styles.downloadButton}
            onPress={downloadReportCard}
            disabled={downloading}
          >
            {downloading ? (
              <ActivityIndicator color="#1565C0" size="small" />
            ) : (
              <Text style={styles.downloadText}>{t('downloadReportCard')}</Text>
            )}
          </TouchableOpacity>
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
  downloadButton: {
    marginTop: 12,
    backgroundColor: '#E8F0FB',
    borderRadius: 10,
    padding: 12,
    alignItems: 'center',
  },
  downloadText: { color: '#1565C0', fontWeight: '600', fontSize: 14 },
  downloadError: { color: '#C62828', marginTop: 8, fontSize: 13 },
});
