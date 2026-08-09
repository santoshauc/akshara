import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { FamilyFeeSummary } from '../../api/types';
import { useI18n } from '../../i18n';

const rupees = (value: number) => `₹${value.toLocaleString('en-IN')}`;

/**
 * The whole family at a glance: one balance line per child plus the combined
 * total — shown only when the parent has more than one enrolled child.
 */
export default function FamilyFeesCard({ family }: { family: FamilyFeeSummary }) {
  const { t } = useI18n();
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('familyFeesTitle')}</Text>
      {family.children.map((child) => (
        <View key={child.studentId} style={styles.row}>
          <View style={styles.info}>
            <Text style={styles.name}>{child.studentName}</Text>
            <Text style={styles.meta}>{child.className ?? '—'}</Text>
          </View>
          <Text style={[styles.balance, child.balance === 0 && styles.settled]}>
            {child.balance === 0 ? t('familySettled') : rupees(child.balance)}
          </Text>
        </View>
      ))}
      <View style={[styles.row, styles.totalRow]}>
        <Text style={styles.totalLabel}>{t('familyTotal')}</Text>
        <Text style={[styles.totalValue, family.familyBalance === 0 && styles.settled]}>
          {family.familyBalance === 0 ? t('allPaid') : rupees(family.familyBalance)}
        </Text>
      </View>
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
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
  },
  info: { flex: 1 },
  name: { fontSize: 14, fontWeight: '600' },
  meta: { fontSize: 12, color: '#667' },
  balance: { fontSize: 15, fontWeight: '700', color: '#B26A00' },
  settled: { color: '#2E7D32' },
  totalRow: { borderTopWidth: 2, borderTopColor: '#E1E5EC', marginTop: 4 },
  totalLabel: { flex: 1, fontSize: 14, fontWeight: '700' },
  totalValue: { fontSize: 17, fontWeight: '800', color: '#B26A00' },
});
