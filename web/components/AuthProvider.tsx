"use client";
import React, { createContext, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import toast from "react-hot-toast";
import { tryRestoreSession, setSessionExpiredHandler, type Session } from "../lib/auth";

interface AuthContextValue {
  session:    Session | null;
  setSession: (s: Session | null) => void;
  loading:    boolean;
}

const AuthContext = createContext<AuthContextValue>({
  session:    null,
  setSession: () => {},
  loading:    true,
});

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();
  const t = useTranslations("session");

  useEffect(() => {
    tryRestoreSession()
      .then((s) => setSession(s))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    setSessionExpiredHandler(() => {
      setSession(null);
      toast.error(t("expired"));
      router.replace("/login");
    });
  }, [router, t]);

  return (
    <AuthContext.Provider value={{ session, setSession, loading }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
