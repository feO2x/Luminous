# AGENTS.md for Tests

## General Rules

- Please do not use mocking frameworks like Moq or NSubstitute for test doubles, use hand-crafted test doubles instead.
- Do not write nested test classes. All tests should reside in a class which is directly placed in a namespace.
- Use FluentAssertions instead of xunit's `Assert` class.
- When writing Unit Tests (i.e., tests that only run in-memory and make no I/O calls to third-party systems), prefer Sociable Tests instead of Solitary Tests (according to Martin Fowler's definition). Create as much test coverage as possible by calling higher level production APIs. Only write solitary tests to cover otherwise unreachable lower level APIs - for example, GuardClauses.
- During Integration Tests, at least one I/O call to third-party systems like a database or Web API is made. Some of the third-party system calls can be replaced with Test Doubles or Fakes (according to XUnit Test Patterns by Gerard Meszaros).
- In End-to-End (E2E) Tests, I/O calls must not be replaced with Test Doubles or Fakes.
- Keep test coverage at least above 90%.
