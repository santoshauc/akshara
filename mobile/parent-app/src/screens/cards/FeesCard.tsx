import React, { useState } from 'react';
import { ActivityIndicator, Linking, Platform, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { parentApi } from '../../api/parent';
import { FeeSummary } from '../../api/types';
import { useI18n } from '../../i18n';

const inr = (value: number) => `₹${value.toLocaleString('en-IN')}`;

/** Fee ledger: balance headline, due lines, recent receipts, online payment. */
export default function FeesCard({
  fees,
  studentId,
  onPaymentStarted,
}: {
  fees: FeeSummary | null;
  studentId: string;
  onPaymentStarted?: () => void;
}) {
  const { t } = useI18n();
  const [paying, setPaying] = useState(false);
  const [payInfo, setPayInfo] = useState<string | null>(null);

  const payNow = async () => {
    if (!fees || fees.balance <= 0) {
      return;
    }
    setPaying(true);
    setPayInfo(null);
    try {
      const order = await parentApi.createFeeOrder(studentId, fees.balance);
      if (Platform.OS === 'web') {
        window.open(order.checkoutUrl, '_blank');
      } else {
        await Linking.openURL(order.checkoutUrl);
      }
      setPayInfo(t('paymentOpened'));
      onPaymentStarted?.();
    } catch {
      setPayInfo(t('paymentFailed'));
    } finally {
      setPaying(false);
    }
  };
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
              <Text style={styles.head}>
                {line.feeHeadName}
                {line.label ? ` — ${line.label}` : ''}
              </Text>
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
          {fees.balance > 0 && (
            <TouchableOpacity style={styles.payButton} onPress={payNow} disabled={paying}>
              {paying ? (
                <ActivityIndicator color="#1565C0" size="small" />
              ) : (
                <Text style={styles.payText}>{t('payNow', { amount: inr(fees.balance) })}</Text>
              )}
            </TouchableOpacity>
          )}
          {payInfo && <Text style={styles.payInfo}>{payInfo}</Text>}
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
  payButton: {
    backgroundColor: '#E8F0FB',
    borderRadius: 10,
    padding: 12,
    alignItems: 'center',
    marginBottom: 10,
  },
  payText: { color: '#1565C0', fontWeight: '600', fontSize: 14 },
  payInfo: { color: '#667', fontSize: 12, marginBottom: 8 },
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
