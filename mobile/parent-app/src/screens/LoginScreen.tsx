import React, { useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { requestOtp, verifyOtp } from '../api/parent';
import { BRAND } from '../config';

interface Props {
  onSignedIn: () => void;
}

/** School code + phone → OTP → session. */
export default function LoginScreen({ onSignedIn }: Props) {
  const [schoolCode, setSchoolCode] = useState('');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [stage, setStage] = useState<'details' | 'code'>('details');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sendCode = async () => {
    if (!schoolCode.trim() || !phone.trim()) {
      setError('Enter your school code and phone number.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await requestOtp(schoolCode.trim().toUpperCase(), phone.trim());
      setStage('code');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  const confirmCode = async () => {
    if (code.trim().length !== 6) {
      setError('Enter the 6-digit code from the SMS.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await verifyOtp(schoolCode.trim().toUpperCase(), phone.trim(), code.trim());
      onSignedIn();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <View style={styles.card}>
        <Text style={styles.logo}>🎓</Text>
        <Text style={styles.title}>SchoolErp Parent</Text>
        <Text style={styles.subtitle}>Stay close to your child's school day</Text>

        {stage === 'details' ? (
          <>
            <TextInput
              style={styles.input}
              placeholder="School code (e.g. DEMO01)"
              autoCapitalize="characters"
              value={schoolCode}
              onChangeText={setSchoolCode}
            />
            <TextInput
              style={styles.input}
              placeholder="Mobile number (+91…)"
              keyboardType="phone-pad"
              value={phone}
              onChangeText={setPhone}
            />
            {error && <Text style={styles.error}>{error}</Text>}
            <TouchableOpacity style={styles.button} onPress={sendCode} disabled={busy}>
              {busy ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text style={styles.buttonText}>Send code</Text>
              )}
            </TouchableOpacity>
          </>
        ) : (
          <>
            <Text style={styles.hint}>We sent a 6-digit code to {phone}</Text>
            <TextInput
              style={[styles.input, styles.codeInput]}
              placeholder="••••••"
              keyboardType="number-pad"
              maxLength={6}
              value={code}
              onChangeText={setCode}
            />
            {error && <Text style={styles.error}>{error}</Text>}
            <TouchableOpacity style={styles.button} onPress={confirmCode} disabled={busy}>
              {busy ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text style={styles.buttonText}>Sign in</Text>
              )}
            </TouchableOpacity>
            <TouchableOpacity onPress={() => setStage('details')}>
              <Text style={styles.link}>Change number</Text>
            </TouchableOpacity>
          </>
        )}
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', backgroundColor: '#F4F6FA', padding: 24 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 16,
    padding: 24,
    shadowColor: '#000',
    shadowOpacity: 0.08,
    shadowRadius: 12,
    elevation: 3,
  },
  logo: { fontSize: 40, textAlign: 'center' },
  title: { fontSize: 22, fontWeight: '700', textAlign: 'center', marginTop: 8 },
  subtitle: { fontSize: 14, color: '#667', textAlign: 'center', marginBottom: 24 },
  input: {
    borderWidth: 1,
    borderColor: '#D5DAE3',
    borderRadius: 10,
    padding: 14,
    fontSize: 16,
    marginBottom: 12,
  },
  codeInput: { textAlign: 'center', fontSize: 24, letterSpacing: 8 },
  hint: { fontSize: 14, color: '#667', textAlign: 'center', marginBottom: 12 },
  button: {
    backgroundColor: BRAND,
    borderRadius: 10,
    padding: 15,
    alignItems: 'center',
    marginTop: 4,
  },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
  link: { color: BRAND, textAlign: 'center', marginTop: 16 },
  error: { color: '#C62828', marginBottom: 8, textAlign: 'center' },
});
