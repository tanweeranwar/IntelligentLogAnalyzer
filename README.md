# Intelligent Incident Analyzer

> **AI-powered Production Support Copilot for .NET Applications**

Intelligent Incident Analyzer is an AI-assisted production support platform designed to help Principal Software Engineers (PSEs), Support Engineers, and DevOps teams investigate production incidents faster.

Instead of simply parsing log files, the platform correlates application logs, Windows Event Viewer entries, and application architecture knowledge to produce actionable incident analysis.

The long-term vision is to reduce **Mean Time to Resolution (MTTR)** by providing engineers with context-aware investigation guidance rather than raw log data.

---

# Features

## Intelligent Log Parsing

- Plain Text Application Logs
- Windows Event Viewer Log Support
- Automatic Parser Detection
- Multi-line Exception Parsing
- Timestamp Detection
- HTTP Status Detection
- Exception Detection
- API Detection
- Correlation ID Extraction
- Environment Detection
- Server Detection

---

## Incident Detection

- Error Normalization
- Duplicate Error Detection
- Incident Correlation
- Production Health Calculation
- Error Summaries
- Most Impacted Server
- Most Impacted API
- Most Common Exception
- Average Incident Duration

---

## Application Context Awareness

The analyzer loads application knowledge from a structured JSON knowledge base.

Current capabilities include:

- Application Components
- Controllers
- APIs
- Business Workflows
- Downstream Dependencies
- Database Objects
- Investigation Hints
- Known Production Issues

This enables incidents to be mapped to the correct application workflow instead of treating them as isolated exceptions.

---

# Solution Architecture

```text
               Upload Logs
                     │
                     ▼
          Parser Resolver
          ┌──────────────┐
          │ Plain Text   │
          │ Event Viewer │
          └──────────────┘
                     │
                     ▼
        Normalized Log Entries
                     │
                     ▼
        Shared Analysis Pipeline
                     │
       ┌─────────────┴─────────────┐
       ▼                           ▼
 Incident Builder          Health Calculator
       │                           │
       └─────────────┬─────────────┘
                     ▼
          Incident Intelligence
                     │
                     ▼
     Application Context Resolver
                     │
                     ▼
          Knowledge Base (JSON)
                     │
                     ▼
           Investigation Dashboard
```

---

# Project Structure

```text
LogAnalyzer.Domain
│
├── Models
└── Contracts

LogAnalyzer.Application
│
└── Interfaces

LogAnalyzer.Infrastructure
│
├── Parsers
├── Services
├── Intelligence
└── Context

LogAnalyzer.Web
│
├── Components
├── Pages
└── Knowledge
```

---

# Knowledge Base

The analyzer uses a JSON-based application knowledge model.

```text
Knowledge/
    application-architecture.json
```

The knowledge base currently supports:

- Application Components
- APIs
- Business Workflows
- Known Issues
- Investigation Hints
- Dependencies
- Database Objects

Future versions will automatically generate this knowledge from:

- Source Code
- Architecture Documentation
- Database Metadata
- Runbooks
- Previous Production Incidents

---

# Current Workflow

```text
Upload Log
      │
      ▼
Automatic Parser Detection
      │
      ▼
Normalize Log Entries
      │
      ▼
Correlate Related Errors
      │
      ▼
Build Incidents
      │
      ▼
Calculate Production Health
      │
      ▼
Resolve Application Context
      │
      ▼
Display Investigation Dashboard
```

---

# Roadmap

## ✅ Phase 1 — Foundation (Completed)

- Plain Text Parser
- Windows Event Viewer Parser
- Automatic Parser Selection
- Shared Analysis Pipeline
- Incident Grouping
- Production Health Dashboard
- Application Knowledge Base
- Application Context Resolver

---

## 🚧 Phase 2 — Production Support Copilot (In Progress)

The next phase focuses on helping Production Support Engineers investigate incidents using AI.

### Planned Features

- AI Investigation Engine
- Application-aware Incident Analysis
- Root Cause Recommendations
- Workflow Identification
- Dependency Mapping
- Investigation Checklist
- SQL Suggestions
- Code Location Recommendations
- Intelligent Resolution Guidance

---

## 🔮 Phase 3 — Enterprise Knowledge Integration

The platform will integrate with enterprise systems such as:

- ServiceNow
- Azure DevOps
- GitHub
- Architecture Documentation
- Runbooks
- Deployment History
- Previous Incident Knowledge
- Database Schema Metadata

This will allow AI to understand not only logs but also the application architecture and operational history.

---

## 🚀 Phase 4 — AI Production Support Copilot

The long-term goal is to provide an AI assistant capable of answering questions such as:

- Why did this incident occur?
- Which workflow failed?
- Which downstream dependency is responsible?
- Which code should I inspect first?
- Which SQL queries should I execute?
- Has this issue occurred before?
- What is the most likely root cause?
- What should I verify before escalating?

---

# Vision

The objective of this project is **not** to become another log viewer.

The objective is to build an **AI-powered Production Support Copilot** that understands:

- Application Architecture
- Business Workflows
- APIs
- Databases
- Source Code
- Production Logs
- Historical Incidents
- Internal Knowledge

By combining these information sources, the platform will help engineers move from:

> "I have thousands of log lines."

to

> "I understand what failed, why it likely failed, where to investigate, and what to do next."

---

# Technology Stack

- .NET 9
- C#
- ASP.NET Core Blazor Server
- Dependency Injection
- JSON Knowledge Base
- Extensible Parser Architecture
- AI-ready Investigation Pipeline

---

# Future Vision

The end goal is to evolve Intelligent Incident Analyzer into an enterprise-grade **Production Support Copilot** capable of combining:

- Application Logs
- Event Viewer Logs
- Source Code
- Architecture Documents
- ServiceNow Knowledge
- Azure DevOps Work Items
- Database Metadata
- Deployment History
- AI-powered Reasoning

to dramatically reduce production incident investigation time and improve operational efficiency.