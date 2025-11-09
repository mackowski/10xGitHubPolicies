# Pre-Push Test Suite

Run the complete pre-push test suite and provide a summary.

## Instructions

1. Execute the pre-push test script: `./pre-push-test.sh`
2. Monitor the output and wait for completion
3. After the script completes, provide a summary:
   - If exit code is 0: ✅ All tests passed - **SAFE TO PUSH** changes to repository
   - If exit code is non-zero: ❌ Some tests failed - **DO NOT PUSH** until issues are fixed
4. List any failed test categories if applicable
5. Mention where test results are saved (`./TestResults/` directory)
6. Suggest what to do to fix issues

## What the Script Does

1. **Checks Docker database**: Verifies Docker is running and the `sql-server-db` container is up (starts it if needed)
2. Runs `./test-workflow-local.sh` (linting, unit, component, integration, contract tests)
3. Starts the application with `TestMode:Enabled=true` (no authentication)
4. Runs E2E-Smoke and E2E-Workflow tests
5. Stops the application
6. Starts the application with `TestMode:Enabled=false` (with authentication)
7. Runs E2E-Auth tests
8. Stops the application
9. Provides a final summary

The script automatically handles:
- Starting/stopping the Docker database container if needed
- Starting/stopping the application and waiting for it to be ready
- Error handling and cleanup on exit

