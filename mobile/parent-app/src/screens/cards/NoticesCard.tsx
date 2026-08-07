import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Notice } from '../../api/types';
import { useI18n } from '../../i18n';

/** School notices: pinned first, newest first. */
export default function NoticesCard({ notices }: { notices: Notice[] }) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('noticesTitle')}</Text>
      {notices.length === 0 ? (
        <Text style={styles.muted}>{t('noticesEmpty')}</Text>
      ) : (
        notices.slice(0, 5).map((notice) => (
          <View key={notice.id} style={styles.notice}>
            <Text style={styles.noticeTitle}>
              {notice.isPinned ? '📌 ' : ''}
              {notice.title}
            </Text>
            <Text style={styles.noticeBody}>{notice.body}</Text>
            <Text style={styles.noticeDate}>
              {new Date(notice.publishedAt).toLocaleDateString('en-IN', {
                day: '2-digit',
                month: 'short',
              })}
              {notice.schoolClassId ? ` · ${t('yourClass')}` : ` · ${t('wholeSchool')}`}
            </Text>
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
  notice: {
    paddingVertical: 8,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
  },
  noticeTitle: { fontSize: 14, fontWeight: '600' },
  noticeBody: { fontSize: 13, color: '#445', marginTop: 2 },
  noticeDate: { fontSize: 12, color: '#889', marginTop: 4 },
});
