"use client";
import React, { createContext, useContext, useEffect, useState } from "react";
import { tryRestoreSession, type Session } from "../lib/auth";

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

  useEffect(() => {
    tryRestoreSession()
      .then((s) => setSession(s))
      .finally(() => setLoading(false));
  }, []);

  return (
    <AuthContext.Provider value={{ session, setSession, loading }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
