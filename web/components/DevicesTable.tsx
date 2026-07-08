"use client";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import type { DeviceSummaryResponse } from "../lib/types";

interface DevicesTableProps {
  devices: DeviceSummaryResponse[];
  onRevoke: (device: DeviceSummaryResponse) => void;
}

export function DevicesTable({ devices, onRevoke }: DevicesTableProps) {
  const t = useTranslations("devices");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnLocation")}</TableHead>
          <TableHead>{t("columnGroup")}</TableHead>
          <TableHead>{t("columnPairedBy")}</TableHead>
          <TableHead>{t("columnPairedAt")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {devices.map((device) => {
          const revoked = Boolean(device.revokedAt);
          return (
            <TableRow key={device.id}>
              <TableCell className="font-medium">{device.locationName}</TableCell>
              <TableCell>{device.groupName}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{device.pairedByName}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">
                {new Date(device.pairedAt).toLocaleDateString()}
              </TableCell>
              <TableCell>
                <Badge variant={revoked ? "danger" : "success"}>
                  {revoked ? t("statusRevoked") : t("statusActive")}
                </Badge>
              </TableCell>
              <TableCell className="text-right">
                {!revoked && (
                  <Button variant="destructive" size="sm" onClick={() => onRevoke(device)}>
                    {t("actionRevoke")}
                  </Button>
                )}
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
