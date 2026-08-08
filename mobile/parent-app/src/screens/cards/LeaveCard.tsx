import React, { useState } from 'react';
import { StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import { parentApi } from '../../api/parent';
import { LeaveRequest } from '../../api/types';
import { BRAND } from '../../config';
import { useI18n } from '../../i18n';

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });

const DATE_SHAPE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * Leave requests for the selected child: history with status chips and a
 * small form to file a new request (reviewed by school staff in the portal).
 */
export default function LeaveCard({
  requests,
  studentId,
  onSubmitted,
}: {
  requests: LeaveRequest[];
  studentId: string;
  onSubmitted: () => void;
}) {
  const { t } = useI18n();
  const [formOpen, setFormOpen] = useState(false);
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const statusLabel = (status: number) =>
    status === 2 ? t('leaveApproved') : status === 3 ? t('leaveRejected') : t('leavePending');

  const submit = async () => {
    if (!DATE_SHAPE.test(fromDate) || !DATE_SHAPE.test(toDate) || !reason.trim()) {
      setMessage(t('leaveInvalid'));
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      await parentApi.submitLeaveRequest(studentId, fromDate, toDate, reason.trim());
      setMessage(t('leaveSubmitted'));
      setFormOpen(false);
      setFromDate('');
      setToDate('');
      setReason('');
      onSubmitted();
    } catch {
      setMessage(t('leaveFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('leaveTitle')}</Text>

      {requests.length === 0 && <Text style={styles.muted}>{t('leaveEmpty')}</Text>}
      {requests.slice(0, 4).map((leave) => (
        <View key={leave.id} style={styles.row}>
          <View style={styles.info}>
            <Text style={styles.dates}>
              {formatDate(leave.fromDate)} – {formatDate(leave.toDate)}
            </Text>
            <Text style={styles.reason} numberOfLines={1}>
              {leave.reason}
              {leave.decisionNote ? ` · ${leave.decisionNote}` : ''}
            </Text>
          </View>
          <Text
            style={[
              styles.status,
              leave.status === 2 && styles.approved,
              leave.status === 3 && styles.rejected,
            ]}
          >
            {statusLabel(leave.status)}
          </Text>
        </View>
      ))}

      {message && <Text style={styles.message}>{message}</Text>}

      {formOpen ? (
        <View style={styles.form}>
          <TextInput
            style={styles.input}
            placeholder={t('leaveFrom')}
            value={fromDate}
            onChangeText={setFromDate}
            autoCapitalize="none"
          />
          <TextInput
            style={styles.input}
            placeholder={t('leaveTo')}
            value={toDate}
            onChangeText={setToDate}
            autoCapitalize="none"
          />
          <TextInput
            style={styles.input}
            placeholder={t('leaveReason')}
            value={reason}
            onChangeText={setReason}
          />
          <TouchableOpacity style={styles.button} onPress={submit} disabled={busy}>
            <Text style={styles.buttonText}>{t('leaveSubmit')}</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <TouchableOpacity style={styles.button} onPress={() => setFormOpen(true)}>
          <Text style={styles.buttonText}>{t('requestLeave')}</Text>
        </TouchableOpacity>
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
  info: { flex: 1 },
  dates: { fontSize: 14, fontWeight: '600' },
  reason: { fontSize: 12, color: '#667' },
  status: { fontSize: 12, fontWeight: '700', color: '#B26A00' },
  approved: { color: '#2E7D32' },
  rejected: { color: '#C62828' },
  message: { marginTop: 10, color: '#1565C0', fontSize: 13 },
  form: { marginTop: 12, gap: 8 },
  input: {
    borderWidth: 1,
    borderColor: '#E1E5EC',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
  },
  button: {
    marginTop: 12,
    backgroundColor: BRAND,
    borderRadius: 10,
    paddingVertical: 12,
    alignItems: 'center',
  },
  buttonText: { color: '#fff', fontWeight: '700' },
});
