import React, { useState } from 'react';
import { StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import { parentApi } from '../../api/parent';
import { StudentMessage } from '../../api/types';
import { BRAND } from '../../config';
import { useI18n } from '../../i18n';

const formatTime = (value: string) =>
  new Date(value).toLocaleDateString('en-IN', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });

/**
 * The parent↔school conversation for the selected child: latest messages
 * (chat-style bubbles) and a composer. Staff replies arrive via the portal.
 */
export default function MessagesCard({
  messages,
  studentId,
  onSent,
}: {
  messages: StudentMessage[];
  studentId: string;
  onSent: () => void;
}) {
  const { t } = useI18n();
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const send = async () => {
    if (!draft.trim()) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await parentApi.sendMessage(studentId, draft.trim());
      setDraft('');
      onSent();
    } catch {
      setError(t('messageFailed'));
    } finally {
      setBusy(false);
    }
  };

  const recent = messages.slice(-6);
  return (
    <View style={styles.card}>
      <Text style={styles.title}>{t('messagesTitle')}</Text>
      {recent.length === 0 && <Text style={styles.muted}>{t('messagesEmpty')}</Text>}
      {recent.map((message) => (
        <View
          key={message.id}
          style={[styles.bubble, message.sentByStaff ? styles.fromSchool : styles.fromMe]}
        >
          <Text style={styles.body}>{message.body}</Text>
          <Text style={styles.meta}>
            {message.sentByStaff ? message.senderName : t('you')} · {formatTime(message.sentAt)}
          </Text>
        </View>
      ))}
      {error && <Text style={styles.error}>{error}</Text>}
      <View style={styles.composer}>
        <TextInput
          style={styles.input}
          placeholder={t('messagePlaceholder')}
          value={draft}
          onChangeText={setDraft}
          multiline
        />
        <TouchableOpacity style={styles.send} onPress={send} disabled={busy}>
          <Text style={styles.sendText}>{t('messageSend')}</Text>
        </TouchableOpacity>
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
  muted: { color: '#667' },
  bubble: { borderRadius: 12, padding: 10, marginBottom: 8, maxWidth: '85%' },
  fromSchool: { backgroundColor: '#E3F2FD', alignSelf: 'flex-start' },
  fromMe: { backgroundColor: '#F4F6FA', alignSelf: 'flex-end' },
  body: { fontSize: 14 },
  meta: { fontSize: 10, color: '#667', marginTop: 4 },
  error: { color: '#C62828', marginTop: 6 },
  composer: { flexDirection: 'row', gap: 8, marginTop: 10, alignItems: 'flex-end' },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#E1E5EC',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 14,
    maxHeight: 90,
  },
  send: {
    backgroundColor: BRAND,
    borderRadius: 10,
    paddingVertical: 10,
    paddingHorizontal: 16,
  },
  sendText: { color: '#fff', fontWeight: '700' },
});
