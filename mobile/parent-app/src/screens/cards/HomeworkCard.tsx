import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Homework } from '../../api/types';

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });

/** Homework for the child's class/section, nearest due date first. */
export default function HomeworkCard({ homework }: { homework: Homework[] }) {
  return (
    <View style={styles.card}>
      <Text style={styles.title}>Homework</Text>
      {homework.length === 0 ? (
        <Text style={styles.muted}>No homework assigned. Enjoy the evening! 🎈</Text>
      ) : (
        homework.slice(0, 5).map((item) => (
          <View key={item.id} style={styles.item}>
            <View style={styles.itemHeader}>
              <Text style={styles.subject}>{item.subjectName}</Text>
              <Text style={styles.due}>Due {formatDate(item.dueDate)}</Text>
            </View>
            <Text style={styles.itemTitle}>{item.title}</Text>
            <Text style={styles.instructions}>{item.instructions}</Text>
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
  item: {
    paddingVertical: 8,
    borderTopWidth: 1,
    borderTopColor: '#F0F2F6',
  },
  itemHeader: { flexDirection: 'row', justifyContent: 'space-between' },
  subject: { fontSize: 12, fontWeight: '700', color: '#1565C0' },
  due: { fontSize: 12, color: '#E65100', fontWeight: '600' },
  itemTitle: { fontSize: 14, fontWeight: '600', marginTop: 2 },
  instructions: { fontSize: 13, color: '#445', marginTop: 2 },
});
