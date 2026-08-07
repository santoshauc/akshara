/** Token pair returned by the auth endpoints. */
export interface AuthTokens {
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
}

export interface RouteStop {
  id: string;
  name: string;
  sortOrder: number;
  pickupTime: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface ManifestRider {
  studentId: string;
  studentName: string;
  className: string | null;
  stopName: string;
  stopOrder: number;
}

export type TripType = 1 | 2; // 1 = Pickup, 2 = Drop

export interface DriverRoute {
  routeId: string;
  routeName: string;
  vehicleRegistration: string | null;
  stops: RouteStop[];
  riders: ManifestRider[];
  activeTripId: string | null;
  activeTripType: TripType | null;
}

export type RiderEventType = 1 | 2 | 3; // PickedUp, Dropped, Absent
