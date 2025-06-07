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

## 🔐 Default Login Credentials

**⚠️ Change these immediately after installation!**

| Account Type | Username | Password | Access Level |
|--------------|----------|----------|--------------|
| Master Admin | `admin` | `admin123` | Full access to all features |
| Operator | `user` | `user123` | View reports, adjust volume |

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

### Log Files Location
- **Application Logs**: `%APPDATA%\PhotoBoothX\Logs\`
- **Error Logs**: `photobooth-error-{date}.log`
- **Activity Logs**: `photobooth-activity-{date}.log`

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
1. Change default passwords immediately
2. Use strong passwords (8+ characters, mixed case, numbers)
3. Regular password rotation (monthly recommended)
4. Disable unused accounts

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
| View Logs | Admin Panel → System → View Logs |
| Backup Data | Admin Panel → Backup → Export All |
| Test Hardware | Admin Panel → Diagnostics → Hardware Test |

---

**Keep this reference handy for daily operations!** 📋 