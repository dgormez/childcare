import { useEffect, useState } from "react";
import { useRouter } from "expo-router";
import * as AppleAuthentication from "expo-apple-authentication";
import { applyAuthResponse } from "./auth";
import { storeRefreshToken } from "./api";
import type { AuthResponse } from "../types";

export function useAppleSignIn(baseUrl: string) {
  const router = useRouter();
  const [isAvailable, setIsAvailable] = useState(false);
  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState("");

  useEffect(() => {
    AppleAuthentication.isAvailableAsync()
      .then(setIsAvailable)
      .catch(() => setIsAvailable(false));
  }, []);

  const signIn = async () => {
    setError("");
    setLoading(true);
    try {
      const credential = await AppleAuthentication.signInAsync({
        requestedScopes: [
          AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
          AppleAuthentication.AppleAuthenticationScope.EMAIL,
        ],
      });

      const { identityToken, email } = credential;
      if (!identityToken) throw new Error("Apple Sign In failed — no identity token returned.");

      const res = await fetch(`${baseUrl}/api/auth/apple`, {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ identityToken, email: email ?? undefined }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error((body as any).error ?? "Apple Sign In failed. Please try again.");
      }

      const data: AuthResponse = await res.json();
      await storeRefreshToken(data.refreshToken);
      applyAuthResponse(baseUrl, data);
      router.replace("/(tabs)");
    } catch (e: unknown) {
      const err = e as any;
      // User cancelled the native sheet — silently ignore
      if (err?.code === "ERR_REQUEST_CANCELED") return;
      setError(err?.message ?? "Apple Sign In failed.");
    } finally {
      setLoading(false);
    }
  };

  return {
    signIn,
    clearError:  () => setError(""),
    loading,
    error,
    isAvailable,
  };
}
