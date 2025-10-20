/**
 * Cryptographic utilities for master password generation
 * Matches C# implementation in PhotoBooth/Services/MasterPasswordService.cs
 */

/**
 * PBKDF2 key derivation matching C# implementation
 */
export async function derivePrivateKey(
  baseSecret: string,
  macAddress: string
): Promise<Uint8Array> {
  const data = new TextEncoder().encode(
    baseSecret + "|" + macAddress.toUpperCase()
  );
  const salt = new TextEncoder().encode("PhotoBoothX.MasterPassword.v1");

  // Import key
  const keyMaterial = await crypto.subtle.importKey(
    "raw",
    data,
    { name: "PBKDF2" },
    false,
    ["deriveBits"]
  );

  // Derive 256-bit key using PBKDF2-HMAC-SHA256 with 100,000 iterations
  const derivedBits = await crypto.subtle.deriveBits(
    {
      name: "PBKDF2",
      salt: salt,
      iterations: 100000,
      hash: "SHA-256",
    },
    keyMaterial,
    256 // 32 bytes
  );

  return new Uint8Array(derivedBits);
}

/**
 * HMAC-SHA256 computation
 */
export async function computeHMAC(
  privateKey: Uint8Array,
  data: string
): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey(
    "raw",
    privateKey,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const dataBytes = new TextEncoder().encode(data);
  const signature = await crypto.subtle.sign("HMAC", key, dataBytes);

  return new Uint8Array(signature);
}

/**
 * Generate cryptographically secure 4-digit nonce
 */
export function generateNonce(): string {
  const array = new Uint32Array(1);
  crypto.getRandomValues(array);
  const nonce = (array[0] % 10000).toString().padStart(4, "0");
  return nonce;
}

/**
 * Extract 4 digits from HMAC hash (matches C# implementation)
 */
export function extractHmacDigits(hmac: Uint8Array): string {
  // Convert first 4 bytes to unsigned 32-bit integer (little-endian)
  const dataView = new DataView(hmac.buffer, hmac.byteOffset, 4);
  const u32 = dataView.getUint32(0, true); // true = little-endian

  // Modulo 10000 (unsigned, guaranteed positive, no Math.abs overflow)
  const hmacDigits = (u32 % 10000).toString().padStart(4, "0");

  return hmacDigits;
}

/**
 * Generate 8-digit master password
 */
export async function generateMasterPassword(
  baseSecret: string,
  macAddress: string
): Promise<{ password: string; nonce: string }> {
  // Derive machine-specific private key
  const privateKey = await derivePrivateKey(baseSecret, macAddress);

  // Generate random nonce
  const nonce = generateNonce();

  // Compute HMAC with nonce and MAC address
  const data = nonce + "|" + macAddress.toUpperCase();
  const hmac = await computeHMAC(privateKey, data);

  // Extract 4 digits from HMAC
  const hmacDigits = extractHmacDigits(hmac);

  // Combine nonce + HMAC digits
  const password = nonce + hmacDigits;

  return { password, nonce };
}

