# Mod Toolkit design

A compact racing garage for everyday players. Use the supplied logo, restrained neutral light/dark surfaces and orange actions. Keep the library focused on search, categories and enabled modules; expandable settings have readable defaults, resets and override controls. Do not add nested cards or decorative dashboard metrics.

React pages share controls and one Tailwind/theme stylesheet. Related page handlers stay together; avoid one file per event or CSS selector. Navigation becomes a wrapping top bar below tablet width. Fields stack without horizontal overflow. Use SVG chevrons with stable rotation, clear contrast, keyboard focus and reduced-motion support.

Workshop provides a source editor, read-only game API/native evidence, sharing and a first-mod tutorial. Installation follows game discovery, loader readiness and plugin installation. Logs, conflict files and backups stay behind disclosures. Preserve pending edits across navigation and server refreshes. Make loading, failure and saved states explicit.
