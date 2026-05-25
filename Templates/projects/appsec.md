# Project Context: AppSec / Pentest

## Domain
Application security testing, vulnerability research, and secure code review.

## Stack
- Burp Suite Pro / Enterprise
- Python (scripts, extensions)
- Java (Burp extensions)
- Tenable.io / Nessus
- Kali Linux tooling

## Guidelines
- Always consider the attacker perspective.
- Validate all inputs. Flag unsafe deserialization, SSRF, IDOR, and auth bypass.
- Prefer defense-in-depth: input validation + output encoding + parameterized queries.
- When generating PoCs, include mitigation advice.
