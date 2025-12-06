# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability, please follow these steps:

### Do NOT

- Open a public GitHub issue
- Post details on social media
- Share the vulnerability publicly before it's fixed

### Do

1. **Use GitHub's private vulnerability reporting feature** at the Security tab of this repository
2. Or **create a private issue** with the `security` label
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Any suggested fixes

### What to Expect

- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days
- **Fix Timeline**: Depends on severity
  - Critical: 24-48 hours
  - High: 7 days
  - Medium: 30 days
  - Low: Next release

### After the Fix

- We will credit you in the release notes (unless you prefer to remain anonymous)
- We will coordinate disclosure timing with you

## Security Best Practices

When using omap, keep these security considerations in mind:

### Connection Strings

- Never commit connection strings to source control
- Use environment variables or secure vaults
- The tool masks passwords in console output, but logs may contain sensitive data

### Generated Code

- Review generated code before using in production
- Generated code may need additional validation for your security requirements
- SQL parameters are used to prevent injection, but verify for your use case

### File Permissions

- Output directories should have appropriate permissions
- Log files may contain schema information

## Dependencies

We regularly update dependencies to address security vulnerabilities. Run:

```bash
dotnet list package --vulnerable
```

To check for known vulnerabilities in dependencies.
