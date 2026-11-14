# .NET Technical Test: Mortgage Application API

> Source of truth for requirements. Transcribed verbatim from `.NET_Technical_Test-v1_1.docx.pdf` (4 pages), provided by Alessandro on 2026-11-14. Only the formatting was converted to Markdown.

## Overview

Thank you for taking the time to complete this technical exercise.

The goal of this test is to understand how you approach building a small, well-structured application using C# and .NET. We are interested not only in what you build, but how you think about design, structure, and trade-offs.

You are welcome to use any tools you are comfortable with, including AI-assisted tools. During the follow-up interview, we will discuss your implementation and the decisions you made.

## Scenario

You are tasked with building a simple backend service for managing mortgage applications.

The service should allow users to:

- Submit a mortgage application
- Retrieve an existing application
- Perform a basic affordability assessment

## Requirements

### 1. Create a Mortgage Application

Provide an endpoint to create a mortgage application with the following fields:

- Annual income
- Monthly expenses
- Deposit amount
- Property value
- Term (in years)

You should:

- Validate inputs appropriately
- Persist the application

### 2. Retrieve a Mortgage Application

Provide an endpoint to retrieve a previously created application.

### 3. Affordability Assessment

Provide an endpoint to assess affordability for a given application.

The assessment should include:

- Loan-to-value (LTV)
- Debt-to-income ratio
- A decision: approved or declined
- A maximum borrowing estimate
- A short explanation for the decision

You are free to define the exact calculation logic, but it should be reasonable and clearly explained.

The affordability logic does not need to be realistic or comprehensive. We are not assessing domain knowledge in mortgage underwriting.

Focus on a simple, reasonable approach and clearly express any assumptions you make.

## Technical Expectations

We expect an ASP.NET 10 application that demonstrates good use of the framework.

This includes:

- Clear model structure and relationships
- Use of validations
- Thoughtful organisation of business logic
- Clean and readable code

The application should expose a JSON API.

## Testing

Automated tests are required.

At a minimum, include tests for:

- Core business logic (e.g. affordability calculations)
- Any critical validation rules

## Optional Enhancements

These are not required, but may be used to demonstrate additional capability:

- Background processing for assessments
- Authentication
- Docker support
- Logging or instrumentation

## Documentation

Please include a README with:

- Setup instructions
- How to run the application
- How to run tests
- A brief explanation of your design decisions

## Design & Reflection

Please include a short section in your README addressing the following:

### 1. Key Design Decisions

Describe 2-3 key design decisions you made in your implementation and why you chose them.

### 2. Scaling Consideration

If your current implementation needed to handle significantly higher load, what is the first thing you would change, and why?

### 3. Trade-offs

What did you deliberately keep simple or leave out in this implementation?

### 4. Next Steps

If you had more time, what would you improve or add next?

## Submission

Please provide your solution as:

- A GitHub repository (preferred), or
- A compressed archive of the codebase

## Time Expectation

This test is designed to take a few hours.

We are not expecting a production-ready system. Focus on clarity, structure, and demonstrating your approach.

## Follow-Up Discussion

In the next stage, we will walk through your solution together.

Please prepare a short presentation (maximum 3 slides) to support this discussion.

The **total session (presentation + discussion) will be a maximum of 30 minutes**.

Your presentation should be brief and is intended to **kick-start a conversation**, not to be a polished or exhaustive walkthrough.

During the session, we expect you to perform a fresh clone of your repository, build and run your solution, provide a short demonstration, and talk us through the codebase and your implementation choices. Following the demo and discussion, you will present your slides and take us through the key points.

We suggest you cover:

- An overview of your solution and how it is structured
- Key design decisions and why you made them
- How your assessment logic works and any assumptions you made
- What you would change first if this needed to operate at scale

The goal is to have an engaging discussion about your approach, trade-offs, and reasoning. We may ask follow-up questions and explore how your solution could evolve in a real-world scenario.

Good luck, and we look forward to discussing your work.
