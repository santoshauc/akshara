import React from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { BRAND } from '../config';
import { Language, useI18n } from '../i18n';

const OPTIONS: { value: Language; label: string }[] = [
  { value: 'en', label: 'EN' },
  { value: 'te', label: 'తెలుగు' },
];

/** Compact EN/తెలుగు switch; the choice persists across sessions. */
export default function LanguageToggle() {
  const { lang, setLang } = useI18n();
  return (
    <View style={styles.row}>
      {OPTIONS.map((option) => {
        const active = option.value === lang;
        return (
          <TouchableOpacity
            key={option.value}
            style={[styles.chip, active && styles.chipActive]}
            onPress={() => setLang(option.value)}
          >
            <Text style={[styles.label, active && styles.labelActive]}>{option.label}</Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', gap: 4 },
  chip: {
    borderRadius: 12,
    paddingVertical: 4,
    paddingHorizontal: 10,
    backgroundColor: '#EDF0F5',
  },
  chipActive: { backgroundColor: BRAND },
  label: { fontSize: 12, fontWeight: '600', color: '#556' },
  labelActive: { color: '#fff' },
});
