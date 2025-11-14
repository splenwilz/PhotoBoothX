# DNP DS-RX1 HS Roll Capacity Research Summary

## Research Findings

### 1. **No Public SDK/API Available**
- DNP does **NOT** provide a publicly accessible Software Development Kit (SDK) or Application Programming Interface (API) for the DS-RX1 HS printer
- Standard Windows printing APIs (WMI, System.Printing, PrinterSettings) do not expose roll capacity information
- Our current implementation confirms this: WMI queries return standard printer properties but no paper level data

### 2. **DNP Status App Capabilities**
DNP provides a "Status App for Windows OS" that **CAN** read roll capacity information:
- ✅ Media type loaded
- ✅ **Prints remaining** (from RFID chip on ribbon)
- ✅ Printer status
- ✅ Total print count
- ✅ Firmware version
- ✅ Serial number
- ✅ Color control data

**Key Finding**: The Status App reads "prints remaining" from an **RFID chip embedded in the paper ribbon/roll**. This means:
- The capacity data is stored on the physical media itself
- The printer can read this RFID chip
- The Status App communicates with the printer to retrieve this data

### 3. **How It Works**
- The DNP DS-RX1 HS uses **RFID chips on the ribbon/paper rolls** to store capacity information
- The printer hardware can read these RFID chips
- The Status App queries the printer to get this RFID data
- This data is **NOT** exposed through standard Windows printer APIs

### 4. **Current Implementation Status**
Our codebase already attempts:
- ✅ WMI queries (Win32_Printer, Win32_PrinterConfiguration) - **No capacity data found**
- ✅ System.Printing.PrintQueue status flags - **Only basic paper problem detection**
- ✅ PrinterSettings custom properties - **Not available**

**Result**: Standard Windows APIs cannot access the RFID chip data that the Status App can read.

## Possible Solutions

### Option 1: Contact DNP Support (Recommended First Step)
**Action**: Contact DNP technical support directly
- **Why**: They may provide:
  - Developer documentation not publicly listed
  - Proprietary SDK/API access for commercial customers
  - Technical specifications for USB/Serial communication
  - Guidance on accessing RFID chip data programmatically
- **Contact**: DNP Support - https://dnpphoto.com/en-us/Support
- **What to Ask**:
  - "Do you provide a developer SDK or API for accessing roll capacity/paper level information programmatically?"
  - "How does the Status App communicate with the printer to read RFID chip data?"
  - "Is there a way to query paper level via USB/Serial commands?"

### Option 2: Reverse Engineer Status App Communication
**Approach**: Analyze how the Status App communicates with the printer
- Monitor USB/Serial communication between Status App and printer
- Use tools like USB Monitor, Serial Port Monitor, or Wireshark
- Identify command protocol used to query RFID chip data
- **Risks**: 
  - May violate licensing agreements
  - Requires advanced technical expertise
  - Time-consuming
  - May break with driver/firmware updates

### Option 3: Check for COM Interface or Registry Storage
**Approach**: Investigate if Status App exposes interfaces or stores data
- Check if Status App exposes COM interfaces we can call
- Check Windows Registry for stored capacity data
- Check for shared memory or files written by Status App
- **Likelihood**: Low (Status App likely communicates directly with printer)

### Option 4: USB Direct Communication
**Approach**: Attempt direct USB communication with printer
- Use `System.IO.Ports` or USB libraries to communicate directly
- Would need to discover the command protocol
- **Challenge**: No documented protocol available publicly
- **Requires**: Protocol documentation from DNP or reverse engineering

### Option 5: Manual Tracking (Workaround)
**Approach**: Track prints in database and estimate capacity
- Store print count in database
- Estimate remaining capacity based on:
  - Known roll capacity (e.g., 700 prints for 4x6, 1400 for strips)
  - Print count since last roll change
- Allow manual reset when roll is changed
- **Pros**: Works immediately, no external dependencies
- **Cons**: Less accurate, requires manual roll change tracking

## Recommendations

### Immediate Action Plan

1. **Contact DNP Support** (Priority 1)
   - Reach out to DNP technical support
   - Ask about developer resources for roll capacity access
   - Request documentation or SDK access

2. **Implement Manual Tracking** (Priority 2 - Workaround)
   - Add database table to track:
     - Current roll capacity estimate
     - Print count since last roll change
     - Roll change timestamp
   - Add admin UI to reset roll capacity when new roll is installed
   - Display estimated remaining prints based on tracked data
   - This provides immediate value while pursuing Option 1

3. **Investigate Status App** (Priority 3 - If Option 1 fails)
   - Install DNP Status App on test machine
   - Monitor communication between Status App and printer
   - Document findings for potential implementation

### Code Changes Needed (If DNP Provides API)

If DNP provides an SDK or API, we would need to:
1. Add DNP SDK/API NuGet package or DLL reference
2. Create `TryGetRollCapacityViaDNPSDK()` method in `PrinterService.cs`
3. Integrate into existing `GetRollCapacity()` method
4. Update UI to display accurate roll capacity

## Current Status

✅ **Completed**:
- WMI property enumeration (all properties logged)
- PrintQueue status checking
- Driver property checking
- Comprehensive logging for debugging

❌ **Not Available**:
- Roll capacity data via standard Windows APIs
- Public SDK/API from DNP
- Documented USB/Serial communication protocol

## Next Steps

1. **User Decision**: Choose which approach to pursue:
   - Contact DNP Support (recommended)
   - Implement manual tracking workaround
   - Both (workaround now, API later)

2. **If Manual Tracking**: I can implement:
   - Database schema for roll tracking
   - Admin UI for roll management
   - Automatic print count tracking
   - Estimated capacity display

3. **If DNP Provides API**: I can integrate:
   - SDK/API calls into existing `GetRollCapacity()` method
   - Error handling and fallback logic
   - UI updates for accurate display

---

**Research Date**: 2025-01-13  
**Printer Model**: DNP DS-RX1 HS  
**Status**: No public API available, Status App uses RFID chip data

