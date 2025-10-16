# PhotoBoothX Quick Reference

## 🚀 Installation & Setup

### System Requirements
- Windows 10/11 (64-bit)
- 4GB RAM minimum, 8GB recommended
- 500MB available storage
- Administrator privileges for installation

### Quick Install
1. Download `PhotoBoothX-Setup-{version}.exe` from [Releases](../../../releases/latest)
2. Run as Administrator
3. Follow installation wizard
4. Application starts automatically

## 🔐 Initial Setup & Login

**🛡️ Secure First-Time Setup Required**

PhotoBoothX uses a **secure initialization process** to protect your installation:

1. **First Launch**: Application generates random admin credentials
2. **Setup Wizard**: Follow on-screen instructions to create your admin account
3. **Password Requirements**: Strong passwords enforced (12+ characters, mixed case, numbers, symbols)
4. **Account Types**: Master Admin (full access) and Operator (limited access)

### Accessing Admin Panel After Setup
- **Method 1**: Tap 5 times in top-left corner of welcome screen
- **Method 2**: Use credentials created during initial setup

## 🖥️ Admin Panel Access

### How to Access Admin Panel
1. **From Welcome Screen**: Tap 5 times on the top-left corner
2. **Keyboard Shortcut**: Press `Ctrl + Shift + A`

### Admin Panel Features
- **Sales Reports**: View daily, weekly, monthly revenue
- **Template Management**: Upload and manage photo templates
- **User Management**: Change passwords, manage accounts
- **System Settings**: Configure pricing, hardware, display settings
- **Database Backup**: Export sales data and settings

## 💰 Pricing & Products

### Setting Up Products
1. Admin Panel → Products
2. Click "Add Product"
3. Enter name, price, description
4. Set active status
5. Save changes

### Default Product Structure
- **Single Print**: $5.00
- **Double Print**: $8.00
- **Custom Package**: Variable pricing

## 🎨 Template Management

### Template Requirements
- **Format**: PNG, JPG, or PDF
- **Resolution**: 300 DPI for print quality
- **Size**: 4"x6" or 5"x7" recommended (unsure)
- **Color Mode**: RGB or CMYK

### Adding Templates
1. Admin Panel → Templates
2. Click "Upload Template"
3. Select template file
4. Enter name and description
5. Set as active/inactive
6. Test template in selection screen

### Template Location
- **Installation**: `C:\Program Files\PhotoBoothX\Templates\`
- **User-Updatable**: Yes, admin can add/remove templates
- **Backup**: Templates included in system backup

## 🔧 Troubleshooting

### Common Issues

#### Application Won't Start
1. Check Windows Event Viewer for errors
2. Verify .NET 8.0 runtime is installed
3. Run as Administrator
4. Check antivirus blocking application

#### Template Problems
1. Verify template file format (PNG, JPG, PDF)
2. Check file permissions in Templates folder
3. Ensure template resolution is adequate
4. Test template in admin panel first

#### Performance Issues
1. Close unnecessary applications
2. Check available RAM and disk space
3. Update graphics drivers
4. Reduce template file sizes

### System Logs & Diagnostics

**📋 Hybrid Logging System**
PhotoBoothX uses a comprehensive dual-logging approach for maximum reliability:

**📁 File-Based Operational Logs** (Primary)
- **Location**: `%APPDATA%\PhotoboothX\Logs\`
- **Daily Rotation**: Automatic file rotation with retention policies
- **Multiple Log Files**:
  - `application-YYYY-MM-DD.log` - General operations (30 days)
  - `hardware-YYYY-MM-DD.log` - Camera, printer, Arduino (30 days)
  - `transactions-YYYY-MM-DD.log` - Customer interactions (90 days)  
  - `errors-YYYY-MM-DD.log` - All errors and exceptions (60 days)
  - `performance-YYYY-MM-DD.log` - Timing and resource usage (14 days)
- **Debug Files**: Separate debug logs with shorter retention (7 days)

**🗄️ Database Transaction Records** (Secondary)
- **Storage**: Transaction and error tables in `%APPDATA%\PhotoboothX\photobooth.db`
- **Purpose**: Sales transactions, customer data, persistent error tracking
- **Access**: Admin Panel → Reports → Sales Data
- **Retention**: Permanent (included in database backups)

**📊 Log Structure**
- **Levels**: Debug, Info, Warning, Error, Critical
- **Categories**: Application, Hardware, Transactions, Errors, Performance
- **Format**: Structured with timestamp, level, thread, category, message, properties
- **Properties**: Key-value pairs for structured data (durations, IDs, status codes)

**🔍 Troubleshooting**
- **Real-time monitoring**: Tail log files during operation
- **Historical analysis**: Search through rotated files
- **Remote diagnosis**: Copy log files for support analysis
- **Performance tracking**: Monitor operation timing and resource usage

## 📊 Reports & Analytics

### Sales Reports
- **Daily Sales**: Revenue, transaction count, popular templates
- **Weekly Summary**: Trends, peak hours, template usage
- **Monthly Reports**: Revenue targets, growth analysis
- **Export Options**: CSV, Excel, PDF formats

### Template Analytics
- **Usage Statistics**: Most/least popular templates
- **Revenue by Template**: Earnings per template
- **Seasonal Trends**: Template popularity over time

## 🔒 Security & Maintenance

### Password Security
1. **Default Credentials**: 
   - Master Admin: `admin` / `admin123`
   - User Admin: `user` / `user123`
2. **Forced Password Change**: You will be required to change passwords on first login
3. **Strong Passwords**: Use 12+ characters with mixed case, numbers, and symbols
4. **Regular Rotation**: Change passwords monthly for maximum security
5. **Account Management**: Disable unused accounts, review permissions regularly

### Data Backup
1. **Automatic**: Daily database backup to `%APPDATA%\PhotoBoothX\Backups\`
2. **Manual**: Admin Panel → Backup → Export All Data
3. **Schedule**: Weekly full backup recommended
4. **Storage**: External drive or cloud storage

### Regular Maintenance
- **Weekly**: Clean screen and hardware, check paper/supplies
- **Monthly**: Review reports, update templates, backup data
- **Quarterly**: Update software, review security settings
- **Annually**: Deep clean hardware, license renewal

## 🆘 Emergency Procedures

### System Crash Recovery
1. Restart Windows
2. Check Event Viewer for errors
3. Restart PhotoBoothX application
4. If database corrupted, restore from backup
5. Contact support if issues persist

### Hardware Failure
1. Check all USB connections
2. Test hardware with other applications
3. Update device drivers
4. Use admin panel diagnostics
5. Contact hardware vendor if needed

### Data Loss Prevention
1. Enable automatic backups
2. Test backup restoration monthly
3. Keep offline backup copies
4. Document all configuration changes

## 📞 Support Contacts

### Self-Service Resources
- **User Manual**: Built into application (Help menu)
- **Video Tutorials**: Available in admin panel
- **FAQ**: Common questions and solutions
- **Community Forum**: User discussions and tips

### Technical Support
- **Documentation**: [GitHub Documentation](../)
- **Bug Reports**: [GitHub Issues](../../../issues)
- **Feature Requests**: [GitHub Discussions](../../../discussions)

---

## 🎯 Quick Commands Reference

| Action | Method |
|--------|--------|
| Access Admin Panel | `Ctrl + Shift + A` or 5 taps on top-left corner |
| Take Screenshot | `Ctrl + Shift + S` |
| Full Screen Toggle | `F11` |
| Emergency Exit | `Ctrl + Alt + X` |
| Restart Application | `Ctrl + Shift + R` |
| View Sales Data | Admin Panel → Reports → Sales Data |
| View File Logs | Open `%APPDATA%\PhotoboothX\Logs\` folder |
| Backup Data | Admin Panel → Backup → Export All |
| Test Hardware | Admin Panel → Diagnostics → Hardware Test |

---

**Keep this reference handy for daily operations!** 📋 