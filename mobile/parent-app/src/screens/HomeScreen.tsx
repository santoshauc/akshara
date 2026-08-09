import React, { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Image,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { logout, parentApi } from '../api/parent';
import {
  BookLoan,
  Child,
  ChildHostel,
  ChildTransport,
  Exam,
  FamilyFeeSummary,
  FeeSummary,
  Homework,
  LeaveRequest,
  MonthAttendance,
  StudentMessage,
  Notice,
  StudentResult,
  TimetableEntry,
} from '../api/types';
import LanguageToggle from '../components/LanguageToggle';
import { API_BASE_URL, BRAND } from '../config';
import { useI18n } from '../i18n';
import AttendanceCard from './cards/AttendanceCard';
import BusLiveCard from './cards/BusLiveCard';
import FamilyFeesCard from './cards/FamilyFeesCard';
import HostelCard from './cards/HostelCard';
import LeaveCard from './cards/LeaveCard';
import LibraryCard from './cards/LibraryCard';
import MessagesCard from './cards/MessagesCard';
import FeesCard from './cards/FeesCard';
import HomeworkCard from './cards/HomeworkCard';
import NoticesCard from './cards/NoticesCard';
import ResultCard from './cards/ResultCard';
import TimetableCard from './cards/TimetableCard';
import TransportCard from './cards/TransportCard';

interface Props {
  onSignedOut: () => void;
}

/**
 * The parent home: child switcher plus the three cards that answer a parent's
 * daily questions — was my child in school, how are the results, what do I owe.
 */
export default function HomeScreen({ onSignedOut }: Props) {
  const { t } = useI18n();
  const [children, setChildren] = useState<Child[] | null>(null);
  const [selected, setSelected] = useState<Child | null>(null);
  const [attendance, setAttendance] = useState<MonthAttendance | null>(null);
  const [fees, setFees] = useState<FeeSummary | null>(null);
  const [exams, setExams] = useState<Exam[]>([]);
  const [result, setResult] = useState<StudentResult | null>(null);
  const [notices, setNotices] = useState<Notice[]>([]);
  const [homework, setHomework] = useState<Homework[]>([]);
  const [transport, setTransport] = useState<ChildTransport | null>(null);
  const [timetable, setTimetable] = useState<TimetableEntry[]>([]);
  const [libraryLoans, setLibraryLoans] = useState<BookLoan[]>([]);
  const [hostel, setHostel] = useState<ChildHostel | null>(null);
  const [leaveRequests, setLeaveRequests] = useState<LeaveRequest[]>([]);
  const [chatMessages, setChatMessages] = useState<StudentMessage[]>([]);
  const [familyFees, setFamilyFees] = useState<FamilyFeeSummary | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadChildren = useCallback(async () => {
    try {
      const list = await parentApi.getChildren();
      setChildren(list);
      setSelected((current) => current ?? list[0] ?? null);
      setError(null);
      // The combined view only earns its place with 2+ children.
      setFamilyFees(
        list.length > 1 ? await parentApi.getFamilyFees().catch(() => null) : null,
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : t('errLoadChildren'));
      setChildren([]);
    }
  }, [t]);

  const loadChildData = useCallback(async (child: Child) => {
    const now = new Date();
    const [attendanceData, feesData, examList, noticeList, homeworkList, transportData, timetableData, libraryData, hostelData, leaveData, messageData] =
      await Promise.all([
        parentApi
          .getAttendance(child.studentId, now.getFullYear(), now.getMonth() + 1)
          .catch(() => null),
        parentApi.getFees(child.studentId).catch(() => null),
        parentApi.getExams(child.studentId).catch(() => [] as Exam[]),
        parentApi.getNotices(child.studentId).catch(() => [] as Notice[]),
        parentApi.getHomework(child.studentId).catch(() => [] as Homework[]),
        parentApi.getTransport(child.studentId).catch(() => null),
        parentApi.getTimetable(child.studentId).catch(() => [] as TimetableEntry[]),
        parentApi.getLibrary(child.studentId).catch(() => [] as BookLoan[]),
        parentApi.getHostel(child.studentId).catch(() => null),
        parentApi.getLeaveRequests(child.studentId).catch(() => [] as LeaveRequest[]),
        parentApi.getMessages(child.studentId).catch(() => [] as StudentMessage[]),
      ]);
    setAttendance(attendanceData);
    setFees(feesData);
    setExams(examList);
    setNotices(noticeList);
    setHomework(homeworkList);
    setTransport(transportData);
    setTimetable(timetableData);
    setLibraryLoans(libraryData);
    setHostel(hostelData);
    setLeaveRequests(leaveData);
    setChatMessages(messageData);
    const latest = examList[examList.length - 1];
    setResult(
      latest
        ? await parentApi.getResult(child.studentId, latest.id).catch(() => null)
        : null,
    );
  }, []);

  useEffect(() => {
    void loadChildren();
  }, [loadChildren]);

  useEffect(() => {
    if (selected) {
      void loadChildData(selected);
    }
  }, [selected, loadChildData]);

  const onRefresh = async () => {
    setRefreshing(true);
    await loadChildren();
    if (selected) {
      await loadChildData(selected);
    }
    setRefreshing(false);
  };

  const signOut = async () => {
    await logout();
    onSignedOut();
  };

  if (children === null) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={BRAND} />
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
    >
      <View style={styles.header}>
        <Text style={styles.headerTitle}>{t('myChildren')}</Text>
        <View style={styles.headerActions}>
          <LanguageToggle />
          <TouchableOpacity onPress={signOut}>
            <Text style={styles.signOut}>{t('signOut')}</Text>
          </TouchableOpacity>
        </View>
      </View>

      {error && <Text style={styles.error}>{error}</Text>}

      {children.length === 0 && !error && (
        <Text style={styles.empty}>{t('noChildren')}</Text>
      )}

      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.switcher}>
        {children.map((child) => {
          const active = selected?.studentId === child.studentId;
          return (
            <TouchableOpacity
              key={child.studentId}
              style={[styles.childChip, active && styles.childChipActive]}
              onPress={() => setSelected(child)}
            >
              {child.photoUrl ? (
                <Image
                  source={{ uri: `${API_BASE_URL}${child.photoUrl}` }}
                  style={styles.childPhoto}
                />
              ) : (
                <View style={[styles.childPhoto, styles.childPhotoFallback]}>
                  <Text style={[styles.childInitial, active && styles.childNameActive]}>
                    {child.fullName.charAt(0)}
                  </Text>
                </View>
              )}
              <View>
                <Text style={[styles.childName, active && styles.childNameActive]}>
                  {child.fullName}
                </Text>
                <Text style={[styles.childMeta, active && styles.childNameActive]}>
                  {child.className ?? '—'} {child.sectionName ?? ''}
                  {child.rollNumber ? ` · ${t('roll')} ${child.rollNumber}` : ''}
                </Text>
              </View>
            </TouchableOpacity>
          );
        })}
      </ScrollView>

      {selected && (
        <View style={styles.cards}>
          {familyFees && familyFees.children.length > 1 && (
            <FamilyFeesCard family={familyFees} />
          )}
          <AttendanceCard attendance={attendance} />
          <LeaveCard requests={leaveRequests} studentId={selected.studentId}
            onSubmitted={() => void loadChildData(selected)} />
          <TransportCard transport={transport} />
          {transport && <BusLiveCard studentId={selected.studentId} />}
          {hostel && <HostelCard hostel={hostel} />}
          <LibraryCard loans={libraryLoans} />
          <TimetableCard entries={timetable} />
          <HomeworkCard homework={homework} />
          <ResultCard result={result} examCount={exams.length} />
          <FeesCard fees={fees} studentId={selected.studentId}
            onPaymentStarted={() => { setTimeout(() => void loadChildData(selected), 8000); setTimeout(() => void loadChildData(selected), 20000); }} />
          <NoticesCard notices={notices} />
          <MessagesCard messages={chatMessages} studentId={selected.studentId}
            onSent={() => void loadChildData(selected)} />
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F4F6FA' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    paddingTop: 60,
  },
  headerTitle: { fontSize: 24, fontWeight: '700' },
  headerActions: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  signOut: { color: BRAND, fontWeight: '600' },
  switcher: { paddingHorizontal: 16 },
  childChip: {
    backgroundColor: '#fff',
    borderRadius: 12,
    paddingVertical: 10,
    paddingHorizontal: 16,
    marginHorizontal: 4,
    borderWidth: 1,
    borderColor: '#E1E5EC',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  childPhoto: { width: 36, height: 36, borderRadius: 18 },
  childPhotoFallback: {
    backgroundColor: '#E1E5EC',
    justifyContent: 'center',
    alignItems: 'center',
  },
  childInitial: { fontSize: 16, fontWeight: '700', color: '#556' },
  childChipActive: { backgroundColor: BRAND, borderColor: BRAND },
  childName: { fontSize: 15, fontWeight: '600', color: '#223' },
  childMeta: { fontSize: 12, color: '#667' },
  childNameActive: { color: '#fff' },
  cards: { padding: 16, gap: 12 },
  empty: { textAlign: 'center', color: '#667', margin: 24, lineHeight: 20 },
  error: { color: '#C62828', textAlign: 'center', marginHorizontal: 24, marginBottom: 8 },
});
