import { useEffect } from "react";
import * as WebBrowser from "expo-web-browser";

// Handles the OAuth redirect deep link on Android.
// expo-auth-session opens a Chrome Custom Tab for Google/Apple sign-in.
// After the user authenticates, the provider redirects to childcare://oauthredirect.
// Android routes that to this screen via the intent filter in AndroidManifest.xml.
// Calling maybeCompleteAuthSession() here resolves the pending auth session so
// the useAuthRequest hook in the login screen receives the response.
export default function OAuthRedirect() {
  useEffect(() => {
    WebBrowser.maybeCompleteAuthSession();
  }, []);
  return null;
}
