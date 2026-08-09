/**
 * UI strings for the parent app. `en` is the source of truth for keys;
 * `te` (Telugu) must cover every key — the compiler enforces it.
 * Placeholders use {name} and are substituted by t().
 */
export const en = {
  // Login
  appTitle: 'Akshara Parent',
  appSubtitle: "Stay close to your child's school day",
  schoolCodePlaceholder: 'School code (e.g. DEMO01)',
  phonePlaceholder: 'Mobile number (+91…)',
  sendCode: 'Send code',
  signIn: 'Sign in',
  codeSentTo: 'We sent a 6-digit code to {phone}',
  changeNumber: 'Change number',
  errEnterSchoolAndPhone: 'Enter your school code and phone number.',
  errEnterCode: 'Enter the 6-digit code from the SMS.',
  errGeneric: 'Something went wrong.',

  // Home
  myChildren: 'My children',
  signOut: 'Sign out',
  noChildren: 'No children are linked to this phone number yet. Please contact the school office.',
  errLoadChildren: 'Could not load your children.',
  roll: 'Roll',

  // Attendance
  attendanceTitle: 'Attendance this month',
  attendanceEmpty: 'No attendance marked yet this month.',
  present: 'Present',
  absent: 'Absent',
  late: 'Late',
  halfDay: 'Half day',
  leave: 'Leave',

  // Transport
  transportTitle: 'Transport',
  transportEmpty: 'No bus allocation for this child.',
  stop: 'Stop',
  pickup: 'pickup',
  bus: 'Bus',
  callDriver: 'Call {name} ({phone})',
  driver: 'driver',

  // Live bus
  liveBusTitle: 'Live bus',
  liveBusChecking: 'Checking the bus…',
  liveBusIdle: 'The bus is not on a trip right now.',
  pickupInProgress: 'Pickup trip in progress',
  dropInProgress: 'Drop trip in progress',
  started: 'started',
  locationUpdated: 'Location updated {when}',
  justNow: 'just now',
  minuteAgo: '1 minute ago',
  minutesAgo: '{count} minutes ago',
  openInMaps: '🗺️ Open bus location in Maps',
  waitingForGps: 'Waiting for a GPS signal from the bus…',

  // Timetable
  timetableTitle: 'Class timetable',
  timetableEmpty: "The school hasn't published a timetable yet.",
  today: 'today',
  dayMon: 'Monday',
  dayTue: 'Tuesday',
  dayWed: 'Wednesday',
  dayThu: 'Thursday',
  dayFri: 'Friday',
  daySat: 'Saturday',
  daySun: 'Sunday',
  dayMonShort: 'Mon',
  dayTueShort: 'Tue',
  dayWedShort: 'Wed',
  dayThuShort: 'Thu',
  dayFriShort: 'Fri',
  daySatShort: 'Sat',
  daySunShort: 'Sun',

  // Homework
  homeworkTitle: 'Homework',
  homeworkEmpty: 'No homework assigned. Enjoy the evening! 🎈',
  due: 'Due {date}',

  // Results
  resultTitle: 'Latest result',
  resultNonePublished: 'No results published yet.',
  resultNoMarks: 'No marks recorded yet.',
  resultAbsent: 'Absent',
  total: 'Total',
  grade: 'Grade',
  rankOf: 'Rank {rank} of {size}',
  downloadReportCard: '📄 Download report card',
  reportCardFailed: 'Could not download the report card.',

  // Fees
  feesTitle: 'Fees',
  feesEmpty: 'No fee plan for this year yet.',
  allPaid: 'All paid 🎉',
  balance: 'Balance {amount}',
  overdue: 'Overdue',
  dueOn: 'Due {date}',
  payNow: '💳 Pay {amount} online',
  paymentOpened: 'Complete the payment in the opened page, then pull to refresh.',
  paymentFailed: 'Could not start the payment.',

  // Notices
  noticesTitle: 'Notices',
  noticesEmpty: 'No notices right now.',
  yourClass: 'your class',
  wholeSchool: 'whole school',

  // Library
  libraryTitle: 'Library books',
  libraryEmpty: 'No books borrowed right now.',
  dueBack: 'Due back {date}',
  overdueBook: 'Overdue since {date}',
  returned: 'Returned {date}',

  // Hostel
  hostelTitle: 'Hostel',
  hostelRoom: 'Room {room}',
  hostelSince: 'since {date}',
  warden: 'Warden',
  callWarden: 'Call warden ({phone})',

  // Leave
  leaveTitle: 'Leave',
  leaveEmpty: 'No leave requests yet.',
  requestLeave: 'Request leave',
  leaveFrom: 'From (YYYY-MM-DD)',
  leaveTo: 'To (YYYY-MM-DD)',
  leaveReason: 'Reason',
  leaveSubmit: 'Submit request',
  leaveSubmitted: 'Leave request sent. The school will review it.',
  leaveFailed: 'Could not submit the leave request.',
  leaveInvalid: 'Enter valid dates (YYYY-MM-DD) and a reason.',
  leavePending: 'Pending',
  leaveApproved: 'Approved',
  leaveRejected: 'Rejected',

  // Family fees
  familyFeesTitle: 'Family fees',
  familyTotal: 'Family total',
  familySettled: 'Settled ✓',
  insightsTitle: 'How is my child doing?',
  insightsRank: 'Rank',
  insightsChild: 'Your child',
  insightsClass: 'Class avg',
  insightsAttendance: 'Attendance this month',
  insightsFootnote:
    'Comparisons use class averages only — no other child is ever named.',

  // Messages
  messagesTitle: 'Messages',
  messagesEmpty: 'No messages yet. Say hello to the school!',
  messagePlaceholder: 'Write a message to the school…',
  messageSend: 'Send',
  messageFailed: 'Could not send the message.',
  you: 'You',
} as const;

export type TranslationKey = keyof typeof en;

export const te: Record<TranslationKey, string> = {
  // Login
  appTitle: 'అక్షర పేరెంట్',
  appSubtitle: 'మీ పిల్లల స్కూల్ రోజుకు దగ్గరగా ఉండండి',
  schoolCodePlaceholder: 'స్కూల్ కోడ్ (ఉదా. DEMO01)',
  phonePlaceholder: 'మొబైల్ నంబర్ (+91…)',
  sendCode: 'కోడ్ పంపండి',
  signIn: 'సైన్ ఇన్',
  codeSentTo: '{phone} కి 6 అంకెల కోడ్ పంపాము',
  changeNumber: 'నంబర్ మార్చండి',
  errEnterSchoolAndPhone: 'మీ స్కూల్ కోడ్ మరియు ఫోన్ నంబర్ నమోదు చేయండి.',
  errEnterCode: 'SMS లో వచ్చిన 6 అంకెల కోడ్ నమోదు చేయండి.',
  errGeneric: 'ఏదో పొరపాటు జరిగింది.',

  // Home
  myChildren: 'నా పిల్లలు',
  signOut: 'సైన్ అవుట్',
  noChildren: 'ఈ ఫోన్ నంబర్‌కు పిల్లలు ఇంకా లింక్ కాలేదు. దయచేసి స్కూల్ ఆఫీసును సంప్రదించండి.',
  errLoadChildren: 'మీ పిల్లల వివరాలు లోడ్ కాలేదు.',
  roll: 'రోల్',

  // Attendance
  attendanceTitle: 'ఈ నెల హాజరు',
  attendanceEmpty: 'ఈ నెల హాజరు ఇంకా నమోదు కాలేదు.',
  present: 'హాజరు',
  absent: 'గైర్హాజరు',
  late: 'ఆలస్యం',
  halfDay: 'సగం రోజు',
  leave: 'సెలవు',

  // Transport
  transportTitle: 'రవాణా',
  transportEmpty: 'ఈ విద్యార్థికి బస్సు కేటాయింపు లేదు.',
  stop: 'స్టాప్',
  pickup: 'పికప్',
  bus: 'బస్సు',
  callDriver: '{name} కి కాల్ చేయండి ({phone})',
  driver: 'డ్రైవర్',

  // Live bus
  liveBusTitle: 'లైవ్ బస్సు',
  liveBusChecking: 'బస్సును తనిఖీ చేస్తున్నాం…',
  liveBusIdle: 'బస్సు ప్రస్తుతం ప్రయాణంలో లేదు.',
  pickupInProgress: 'పికప్ ప్రయాణం జరుగుతోంది',
  dropInProgress: 'డ్రాప్ ప్రయాణం జరుగుతోంది',
  started: 'ప్రారంభం',
  locationUpdated: 'లొకేషన్ అప్‌డేట్: {when}',
  justNow: 'ఇప్పుడే',
  minuteAgo: '1 నిమిషం క్రితం',
  minutesAgo: '{count} నిమిషాల క్రితం',
  openInMaps: '🗺️ మ్యాప్స్‌లో బస్సు లొకేషన్ చూడండి',
  waitingForGps: 'బస్సు నుంచి GPS సిగ్నల్ కోసం వేచి ఉన్నాం…',

  // Timetable
  timetableTitle: 'తరగతి కాలపట్టిక',
  timetableEmpty: 'స్కూల్ ఇంకా కాలపట్టికను ప్రచురించలేదు.',
  today: 'ఈరోజు',
  dayMon: 'సోమవారం',
  dayTue: 'మంగళవారం',
  dayWed: 'బుధవారం',
  dayThu: 'గురువారం',
  dayFri: 'శుక్రవారం',
  daySat: 'శనివారం',
  daySun: 'ఆదివారం',
  dayMonShort: 'సోమ',
  dayTueShort: 'మంగళ',
  dayWedShort: 'బుధ',
  dayThuShort: 'గురు',
  dayFriShort: 'శుక్ర',
  daySatShort: 'శని',
  daySunShort: 'ఆది',

  // Homework
  homeworkTitle: 'హోంవర్క్',
  homeworkEmpty: 'హోంవర్క్ లేదు. సాయంత్రం ఆనందించండి! 🎈',
  due: 'గడువు {date}',

  // Results
  resultTitle: 'తాజా ఫలితం',
  resultNonePublished: 'ఫలితాలు ఇంకా ప్రచురించబడలేదు.',
  resultNoMarks: 'మార్కులు ఇంకా నమోదు కాలేదు.',
  resultAbsent: 'గైర్హాజరు',
  total: 'మొత్తం',
  grade: 'గ్రేడ్',
  rankOf: 'ర్యాంక్ {rank} / {size}',
  downloadReportCard: '📄 రిపోర్ట్ కార్డ్ డౌన్‌లోడ్ చేయండి',
  reportCardFailed: 'రిపోర్ట్ కార్డ్ డౌన్‌లోడ్ కాలేదు.',

  // Fees
  feesTitle: 'ఫీజులు',
  feesEmpty: 'ఈ సంవత్సరానికి ఫీజు ప్లాన్ ఇంకా లేదు.',
  allPaid: 'అన్నీ చెల్లించారు 🎉',
  balance: 'బకాయి {amount}',
  overdue: 'గడువు దాటింది',
  dueOn: 'గడువు {date}',
  payNow: '💳 {amount} ఆన్‌లైన్‌లో చెల్లించండి',
  paymentOpened: 'తెరిచిన పేజీలో చెల్లింపు పూర్తి చేసి, రిఫ్రెష్ చేయండి.',
  paymentFailed: 'చెల్లింపు ప్రారంభించలేకపోయాం.',

  // Notices
  noticesTitle: 'ప్రకటనలు',
  noticesEmpty: 'ప్రస్తుతం ప్రకటనలు లేవు.',
  yourClass: 'మీ తరగతి',
  wholeSchool: 'మొత్తం స్కూల్',

  // Library
  libraryTitle: 'లైబ్రరీ పుస్తకాలు',
  libraryEmpty: 'ప్రస్తుతం తీసుకున్న పుస్తకాలు లేవు.',
  dueBack: 'తిరిగి ఇవ్వాల్సిన తేదీ {date}',
  overdueBook: '{date} నుంచి గడువు దాటింది',
  returned: '{date} న తిరిగి ఇచ్చారు',

  // Hostel
  hostelTitle: 'హాస్టల్',
  hostelRoom: 'గది {room}',
  hostelSince: '{date} నుంచి',
  warden: 'వార్డెన్',
  callWarden: 'వార్డెన్‌కు కాల్ చేయండి ({phone})',

  // Leave
  leaveTitle: 'సెలవు',
  leaveEmpty: 'సెలవు అభ్యర్థనలు ఇంకా లేవు.',
  requestLeave: 'సెలవు అడగండి',
  leaveFrom: 'నుంచి (YYYY-MM-DD)',
  leaveTo: 'వరకు (YYYY-MM-DD)',
  leaveReason: 'కారణం',
  leaveSubmit: 'అభ్యర్థన పంపండి',
  leaveSubmitted: 'సెలవు అభ్యర్థన పంపబడింది. స్కూల్ సమీక్షిస్తుంది.',
  leaveFailed: 'సెలవు అభ్యర్థన పంపలేకపోయాం.',
  leaveInvalid: 'సరైన తేదీలు (YYYY-MM-DD) మరియు కారణం ఇవ్వండి.',
  leavePending: 'పరిశీలనలో',
  leaveApproved: 'ఆమోదించారు',
  leaveRejected: 'తిరస్కరించారు',

  // Family fees
  familyFeesTitle: 'కుటుంబ ఫీజులు',
  familyTotal: 'కుటుంబం మొత్తం',
  familySettled: 'చెల్లించారు ✓',
  insightsTitle: 'నా పిల్లవాడు ఎలా చదువుతున్నాడు?',
  insightsRank: 'ర్యాంక్',
  insightsChild: 'మీ పిల్లవాడు',
  insightsClass: 'తరగతి సగటు',
  insightsAttendance: 'ఈ నెల హాజరు',
  insightsFootnote:
    'పోలికలు తరగతి సగటులతో మాత్రమే — మరే పిల్లవాడి పేరు ఎప్పుడూ చూపబడదు.',

  // Messages
  messagesTitle: 'సందేశాలు',
  messagesEmpty: 'ఇంకా సందేశాలు లేవు. స్కూల్‌కు హలో చెప్పండి!',
  messagePlaceholder: 'స్కూల్‌కు సందేశం రాయండి…',
  messageSend: 'పంపండి',
  messageFailed: 'సందేశం పంపలేకపోయాం.',
  you: 'మీరు',
};
