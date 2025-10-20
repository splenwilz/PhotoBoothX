# Security Hardening Guide

## Overview

This document outlines the security measures implemented in PhotoBoothX to protect against reverse engineering, credential exposure, and unauthorized access.

---

## 🛡️ Implemented Security Measures

### 1. **Debug Symbol Removal**
**Risk**: PDB files contain full source code mapping, variable names, and method signatures.

**Mitigation**:
- ✅ PDB files are **excluded from Release builds** via `<DebugType>none</DebugType>`
- ✅ All debug symbols stripped during compilation
- ✅ Installer configured to exclude `*.pdb` files

**Files**: `PhotoBooth/PhotoBooth.csproj`, `installer/PhotoBoothX.iss`

---

### 2. **Database Schema Protection**
**Risk**: `Database_Schema.sql` exposes table structures, relationships, and security models.

**Mitigation**:
- ✅ **Embedded as resource** in application DLL via `<EmbeddedResource>` in `.csproj`
- ✅ Schema is read from assembly at runtime via `GetManifestResourceStream()`
- ✅ Not present as separate file in published output or installer
- ✅ SQL file only exists in source code repository

**Implementation**: 
- `PhotoBooth.csproj`: Marks `Database_Schema.sql` as `EmbeddedResource`
- `DatabaseService.cs`: Reads schema from embedded resource during initialization
- No separate `.sql` file shipped with application

---

### 3. **Master Password Config Auto-Deletion**
**Risk**: Plain text config file exposes base secret in installation directory.

**Mitigation**:
- ✅ Config file automatically **deleted on first app launch**
- ✅ Secret immediately encrypted with **Windows DPAPI** and stored in database
- ✅ DPAPI encryption tied to machine + user (cannot be decrypted elsewhere)
- ✅ Config only exists for ~5 seconds during first launch

**Implementation**:
```csharp
// In MasterPasswordConfigService.cs
private void DeleteConfigFile()
{
    var configPath = Path.Combine(baseDir, CONFIG_FILENAME);
    if (File.Exists(configPath))
    {
        File.Delete(configPath);
        LoggingService.Application.Information("Config file deleted for security");
    }
}
```

**Files**: `PhotoBooth/Services/MasterPasswordConfigService.cs`

---

### 4. **Windows DPAPI Encryption**
**Risk**: Storing secrets in database without encryption.

**Mitigation**:
- ✅ Master password base secret encrypted using **Windows Data Protection API**
- ✅ Encryption key tied to **machine + user context**
- ✅ Cannot be decrypted by copying database to another machine
- ✅ Cannot be decrypted by another user on same machine

**Encryption Scope**: `DataProtectionScope.CurrentUser`

**Files**: `PhotoBooth/Services/MasterPasswordConfigService.cs`

---

### 5. **Rate Limiting**
**Risk**: Brute-force attacks on master passwords.

**Mitigation**:
- ✅ **5 failed attempts** = 1-minute lockout
- ✅ Lockout tracked per username in memory
- ✅ Automatic reset after successful authentication
- ✅ Single-use passwords (replay attack prevention)

**Files**: `PhotoBooth/Services/MasterPasswordRateLimitService.cs`

---

### 6. **Single-Use Master Passwords**
**Risk**: Password reuse and replay attacks.

**Mitigation**:
- ✅ Each master password can only be used **once**
- ✅ SHA256 hash stored in `UsedMasterPasswords` table after use
- ✅ Validation checks database before accepting password
- ✅ Includes nonce + MAC address in HMAC for uniqueness

**Files**: `PhotoBooth/Services/DatabaseService.cs`, `Database_Schema.sql`

---

### 7. **Secure Password Generation**
**Risk**: Weak or predictable master passwords.

**Mitigation**:
- ✅ PBKDF2-derived private key (100,000 iterations)
- ✅ HMAC-SHA256 cryptographic signature
- ✅ Random 4-digit nonce per password
- ✅ Machine-specific (derived from MAC address + base secret)

**Algorithm**:
```text
1. privateKey = PBKDF2(baseSecret + macAddress, 100,000 iterations)
2. nonce = RandomNumber(1000-9999)
3. hmac = HMAC-SHA256(privateKey, nonce + macAddress)
4. password = nonce + last4DigitsOf(hmac)
```

**Files**: `PhotoBooth/Services/MasterPasswordService.cs`

---

### 8. **Audit Logging**
**Risk**: Undetected security breaches.

**Mitigation**:
- ✅ All master password attempts logged
- ✅ Failed attempts recorded with timestamp
- ✅ Successful logins tracked with username
- ✅ Rate limit lockouts logged

**Files**: `PhotoBooth/AdminLoginScreen.xaml.cs`

---

## 🚫 Known Limitations

### **Limitation 1: .NET IL Code Exposure**
- **Risk**: .NET assemblies can be decompiled to C# using tools like ILSpy/dnSpy
- **Residual Risk**: Medium
- **Mitigation Options**:
  - ⚠️ Commercial obfuscators (e.g., Dotfuscator, SmartAssembly) - **costs $$$**
  - ⚠️ ConfuserEx (free, but breaks some WPF apps)
  - ✅ **Current approach**: Critical secrets are encrypted, not hardcoded
  
### **Limitation 2: Config File Briefly Visible**
- **Risk**: Config file exists for ~5 seconds on first launch
- **Residual Risk**: Very Low
- **Reality Check**: 
  - Attacker needs file system access during exact 5-second window
  - Even with secret, attacker needs MAC address of target kiosk
  - Passwords are single-use (limited window of opportunity)

### **Limitation 3: Local Database Access**
- **Risk**: User with admin rights can read SQLite database
- **Residual Risk**: Low
- **Mitigation**: 
  - ✅ Secrets encrypted with DPAPI (machine-bound)
  - ✅ Password hashes use PBKDF2 (slow to crack)
  - ✅ Recovery PINs hashed with constant-time comparison

---

## 📋 Security Checklist

### **For Official Releases**:
- [ ] Verify `master-password.config` is **NOT** in version control
- [ ] Confirm PDB files excluded from installer
- [ ] Test config file auto-deletion on first launch
- [ ] Verify database schema file excluded from installer
- [ ] Confirm GitHub secret `MASTER_PASSWORD_BASE_SECRET` is set
- [ ] Test master password generation matches kiosk validation

### **For Development**:
- [ ] Never commit `master-password.config` to git
- [ ] Use strong, random base secrets (62+ characters)
- [ ] Test in isolated VMs before production
- [ ] Review audit logs after security testing

---

## 🎯 Threat Model

| Threat | Likelihood | Impact | Mitigation |
|--------|-----------|--------|------------|
| **Decompiling .NET code** | High | Medium | No hardcoded secrets, DPAPI encryption |
| **Reading config file** | Very Low | High | Auto-deleted on first launch |
| **Database copying** | Low | Low | DPAPI prevents decryption on other machines |
| **Brute force attack** | Medium | Low | Rate limiting + single-use passwords |
| **Replay attack** | Low | None | Single-use password enforcement |
| **Physical access** | High | Variable | Kiosk-specific secrets, encrypted storage |

---

## 🔑 Key Rotation

### **When to Rotate Base Secret**:
1. Suspected compromise
2. Employee termination (if they had access)
3. Annual security review
4. Major version updates

### **How to Rotate**:
1. Generate new 62+ character base secret
2. Update GitHub secret `MASTER_PASSWORD_BASE_SECRET`
3. Trigger new build on `test` branch
4. Test master password generation
5. Deploy to `production` branch
6. Update support team documentation

---

## 📞 Support

For security concerns or questions:
- **Internal**: Review audit logs in database
- **Emergency**: Rotate base secret immediately (see above)
- **Questions**: See `PhotoBooth/docs/master-password-quick-reference.md`

---

## ✅ Conclusion

PhotoBoothX implements **defense in depth**:
1. **Encryption at rest** (DPAPI)
2. **Automatic cleanup** (config deletion)
3. **Single-use passwords** (replay prevention)
4. **Rate limiting** (brute force protection)
5. **Strong cryptography** (PBKDF2 + HMAC-SHA256)
6. **Audit logging** (breach detection)

While no system is perfectly secure, these layers significantly raise the bar for attackers.

