#!/bin/bash

# Local Testing Script - Replicates GitHub Actions Pull Request Workflow
# Usage: ./test-workflow-local.sh

set -e

RESULTS_OUTPUT_DIR="./coverage"

echo "🚀 Starting local workflow test..."

# Create results directory for test logs
mkdir -p "${RESULTS_OUTPUT_DIR}"

# Step 1: Linting
echo ""
echo "📝 Step 1: Linting Code"
echo "======================"
dotnet restore
dotnet format --verify-no-changes --verbosity diagnostic
echo "✅ Linting passed"

# Step 2: Unit Tests
echo ""
echo "🧪 Step 2: Running Unit Tests"
echo "=============================="
dotnet test \
  --filter "Category=Unit" \
  --results-directory "${RESULTS_OUTPUT_DIR}" \
  --logger "trx;LogFileName=unit-tests.trx" \
  --logger "console;verbosity=detailed"

echo "✅ Unit tests completed"

# Step 3: Component Tests
echo ""
echo "🧩 Step 3: Running Component Tests"
echo "==================================="
dotnet test \
  --filter "Category=Component" \
  --results-directory "${RESULTS_OUTPUT_DIR}" \
  --logger "trx;LogFileName=component-tests.trx" \
  --logger "console;verbosity=detailed"

echo "✅ Component tests completed"

# Step 4: Integration Tests
echo ""
echo "🔗 Step 4: Running Integration Tests"
echo "===================================="
echo "ℹ️  Note: Integration tests use WireMock to mock GitHub API (no database required)"
echo ""

dotnet test \
  --filter "Category=Integration" \
  --results-directory "${RESULTS_OUTPUT_DIR}" \
  --logger "trx;LogFileName=integration-tests.trx" \
  --logger "console;verbosity=detailed"

echo "✅ Integration tests completed"

# Step 5: Contract Tests
echo ""
echo "📋 Step 5: Running Contract Tests"
echo "=================================="
dotnet test \
  --filter "Category=Contract" \
  --results-directory "${RESULTS_OUTPUT_DIR}" \
  --logger "trx;LogFileName=contract-tests.trx" \
  --logger "console;verbosity=detailed"

echo "✅ Contract tests completed"

echo ""
echo "🎉 All workflow steps completed successfully!"
echo ""

