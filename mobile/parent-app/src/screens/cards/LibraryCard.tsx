import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { BookLoan } from '../../api/types';
import { useI18n } from '../../i18n';

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });

/** The child's borrowed books: open loans first, overdue flagged. */
export default function LibraryCard({ loans }: { loans: BookLoan[] }) {
  const { t } = useI18n();
  const open = loans.filter((l) => !l.returnedOn);
  const recentReturned = loans.filter((l) => l.returnedOn).slice(0, 2);
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('libraryTitle')}</Text>
      {open.length === 0 && recentReturned.length === 0 ? (
        <Text style={styles.muted}>{t('libraryEmpty')}</Text>
      ) : (
        <>
          {open.map((loan) => (
            <View key={loan.id} style={styles.row}>
              <Text style={styles.emoji}>📖</Text>
              <View style={styles.info}>
                <Text style={styles.bookTitle}>{loan.bookTitle}</Text>
                <Text style={styles.author}>{loan.author}</Text>
              </View>
              <Text style={[styles.due, loan.overdue && styles.overdue]}>
                {loan.overdue
                  ? t('overdueBook', { date: formatDate(loan.dueOn) })
                  : t('dueBack', { date: formatDate(loan.dueOn) })}
              </Text>
            </View>
          ))}
          {recentReturned.map((loan) => (
            <View key={loan.id} style={[styles.row, styles.returnedRow]}>
              <Text style={styles.emoji}>✅</Text>
              <View style={styles.info}>
                <Text style={styles.bookTitle}>{loan.bookTitle}</Text>
              </View>
              <Text style={styles.returnedText}>
                {t('returned', { date: formatDate(loan.returnedOn!) })}
              </Text>
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
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
    gap: 10,
  },
  returnedRow: { opacity: 0.7 },
  emoji: { fontSize: 18 },
  info: { flex: 1 },
  bookTitle: { fontSize: 14, fontWeight: '600' },
  author: { fontSize: 12, color: '#667' },
  due: { fontSize: 12, color: '#1565C0', fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  overdue: { color: '#C62828' },
  returnedText: { fontSize: 12, color: '#2E7D32' },
});
