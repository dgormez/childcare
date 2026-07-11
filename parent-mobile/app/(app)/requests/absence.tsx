import React from "react";
import { DayReservationForm } from "../../../components/DayReservationForm";

export default function AbsenceRequestScreen() {
  return <DayReservationForm type="absence" titleKey="dayReservations.absenceTitle" />;
}
