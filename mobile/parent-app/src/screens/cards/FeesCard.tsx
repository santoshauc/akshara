import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { FeeSummary } from '../../api/types';
import { useI18n } from '../../i18n';

const inr = (value: number) => `₹${value.toLocaleString('en-IN')}`;

/** Fee ledger: balance headline, due lines and recent receipts. */
export default function FeesCard({ fees }: { fees: FeeSummary | null }) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('feesTitle')}</Text>
      {!fees ? (
        <Text style={styles.muted}>{t('feesEmpty')}</Text>
      ) : (
        <>
          <Text style={[styles.balance, fees.balance <= 0 && styles.balanceClear]}>
            {fees.balance <= 0 ? t('allPaid') : t('balance', { amount: inr(fees.balance) })}
          </Text>
          {fees.dueLines.map((line, index) => (
            <View key={`${line.feeHeadName}-${index}`} style={styles.line}>
              <Text style={styles.head}>{line.feeHeadName}</Text>
              <Text style={styles.amount}>{inr(line.amount)}</Text>
              <Text style={[styles.due, line.overdue && styles.overdue]}>
                {line.overdue
                  ? t('overdue')
                  : t('dueOn', {
                      date: new Date(line.dueDate).toLocaleDateString('en-IN', {
                        day: '2-digit',
                        month: 'short',
                      }),
                    })}
              </Text>
            </View>
          ))}
          {fees.payments.slice(0, 3).map((payment) => (
            <View key={payment.id} style={styles.receiptRow}>
              <Text style={styles.receipt}>
                🧾 {payment.receiptNumber} · {inr(payment.amount)} ·{' '}
                {new Date(payment.paidOn).toLocaleDateString('en-IN', {
                  day: '2-digit',
                  month: 'short',
                })}
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
  balance: { fontSize: 22, fontWeight: '800', color: '#E65100', marginBottom: 10 },
  balanceClear: { color: '#2E7D32' },
  line: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 6,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
  },
  head: { flex: 1, fontSize: 14 },
  amount: { width: 90, textAlign: 'right', fontSize: 14, fontWeight: '600' },
  due: { width: 90, textAlign: 'right', color: '#667', fontSize: 12 },
  overdue: { color: '#C62828', fontWeight: '700' },
  receiptRow: { paddingTop: 8 },
  receipt: { color: '#445', fontSize: 13 },
});
