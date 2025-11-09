#!/bin/bash

# Pre-Push Test Script
# Runs all tests including E2E tests with different authentication modes
# Usage: ./pre-push-test.sh

set -e

APP_PROJECT="10xGitHubPolicies.App/10xGitHubPolicies.App.csproj"
BASE_URL="https://localhost:7040"
APP_PID=""
TEST_RESULTS_DIR="./TestResults"
FAILED_STEPS=()

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Cleanup function to ensure app is stopped
cleanup() {
    if [ ! -z "$APP_PID" ] && kill -0 "$APP_PID" 2>/dev/null; then
        echo -e "\n${YELLOW}🛑 Stopping application (PID: $APP_PID)...${NC}"
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
        APP_PID=""
    fi
}

# Trap to ensure cleanup on exit
trap cleanup EXIT INT TERM

# Function to check if Docker database is running
check_docker_database() {
    local container_name="sql-server-db"
    local compose_file="docker-compose.yml"
    
    echo -e "${BLUE}🐳 Checking Docker database status...${NC}"
    
    # Check if Docker is running
    if ! docker info > /dev/null 2>&1; then
        echo -e "${RED}❌ Docker is not running${NC}"
        echo -e "${YELLOW}Please start Docker Desktop and try again${NC}"
        return 1
    fi
    
    # Check if docker-compose.yml exists
    if [ ! -f "$compose_file" ]; then
        echo -e "${RED}❌ docker-compose.yml not found${NC}"
        return 1
    fi
    
    # Detect docker-compose command (V1: docker-compose, V2: docker compose)
    local DOCKER_COMPOSE_CMD="docker-compose"
    if ! command -v docker-compose > /dev/null 2>&1; then
        if docker compose version > /dev/null 2>&1; then
            DOCKER_COMPOSE_CMD="docker compose"
        else
            echo -e "${RED}❌ Neither 'docker-compose' nor 'docker compose' is available${NC}"
            return 1
        fi
    fi
    
    # Check if container exists and is running
    local container_status=$($DOCKER_COMPOSE_CMD ps -q "$container_name" 2>/dev/null)
    
    if [ -z "$container_status" ]; then
        echo -e "${YELLOW}⚠️  Database container '$container_name' is not running${NC}"
        echo -e "${YELLOW}Starting database container...${NC}"
        
        if $DOCKER_COMPOSE_CMD up -d "$container_name"; then
            echo -e "${BLUE}⏳ Waiting for database to be ready...${NC}"
            sleep 5
            
            # Wait for database to be ready (check if it's accepting connections)
            local max_attempts=30
            local attempt=1
            while [ $attempt -le $max_attempts ]; do
                if $DOCKER_COMPOSE_CMD ps "$container_name" | grep -q "Up"; then
                    echo -e "${GREEN}✅ Database container is running${NC}"
                    return 0
                fi
                echo -n "."
                sleep 2
                attempt=$((attempt + 1))
            done
            
            echo -e "\n${RED}❌ Database container failed to start${NC}"
            return 1
        else
            echo -e "${RED}❌ Failed to start database container${NC}"
            return 1
        fi
    else
        # Container exists, check if it's running
        if $DOCKER_COMPOSE_CMD ps "$container_name" | grep -q "Up"; then
            echo -e "${GREEN}✅ Database container is running${NC}"
            return 0
        else
            echo -e "${YELLOW}⚠️  Database container exists but is not running${NC}"
            echo -e "${YELLOW}Starting database container...${NC}"
            
            if $DOCKER_COMPOSE_CMD start "$container_name"; then
                echo -e "${BLUE}⏳ Waiting for database to be ready...${NC}"
                sleep 5
                echo -e "${GREEN}✅ Database container is running${NC}"
                return 0
            else
                echo -e "${RED}❌ Failed to start database container${NC}"
                return 1
            fi
        fi
    fi
}

# Function to wait for app to be ready
wait_for_app() {
    local max_attempts=30
    local attempt=1
    
    echo -e "${BLUE}⏳ Waiting for application to be ready...${NC}"
    
    while [ $attempt -le $max_attempts ]; do
        if curl -k -s -f "$BASE_URL" > /dev/null 2>&1; then
            echo -e "${GREEN}✅ Application is ready!${NC}"
            return 0
        fi
        
        echo -n "."
        sleep 2
        attempt=$((attempt + 1))
    done
    
    echo -e "\n${RED}❌ Application failed to start within 60 seconds${NC}"
    return 1
}

# Function to start the app
start_app() {
    local test_mode=$1
    local mode_description=$2
    
    echo -e "\n${BLUE}🚀 Starting application (TestMode: $test_mode)...${NC}"
    
    # Start app in background
    dotnet run --project "$APP_PROJECT" --launch-profile https --TestMode:Enabled="$test_mode" > /tmp/app-output.log 2>&1 &
    APP_PID=$!
    
    echo -e "${YELLOW}Application started with PID: $APP_PID${NC}"
    echo -e "${YELLOW}Mode: $mode_description${NC}"
    
    # Wait for app to be ready
    if ! wait_for_app; then
        echo -e "${RED}❌ Failed to start application${NC}"
        echo -e "${YELLOW}Last 20 lines of app output:${NC}"
        tail -20 /tmp/app-output.log
        return 1
    fi
    
    return 0
}

# Function to stop the app
stop_app() {
    if [ ! -z "$APP_PID" ] && kill -0 "$APP_PID" 2>/dev/null; then
        echo -e "\n${YELLOW}🛑 Stopping application...${NC}"
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
        APP_PID=""
        sleep 2
        echo -e "${GREEN}✅ Application stopped${NC}"
    fi
}

# Function to run tests
run_tests() {
    local filter=$1
    local test_name=$2
    
    echo -e "\n${BLUE}🧪 Running $test_name tests...${NC}"
    echo -e "${BLUE}Filter: $filter${NC}"
    
    if dotnet test --filter "$filter" \
        --results-directory "$TEST_RESULTS_DIR" \
        --logger "trx;LogFileName=${test_name// /-}.trx" \
        --logger "console;verbosity=normal"; then
        echo -e "${GREEN}✅ $test_name tests passed${NC}"
        return 0
    else
        echo -e "${RED}❌ $test_name tests failed${NC}"
        FAILED_STEPS+=("$test_name")
        return 1
    fi
}

# Create test results directory
mkdir -p "$TEST_RESULTS_DIR"

echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Pre-Push Test Suite${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

# Step 0: Check Docker database
echo -e "\n${BLUE}📋 Step 0: Checking Docker database${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

if ! check_docker_database; then
    echo -e "${RED}❌ Docker database check failed${NC}"
    echo -e "${YELLOW}Please ensure Docker is running and the database container can be started${NC}"
    echo -e "${YELLOW}You can start it manually with: docker-compose up -d${NC}"
    exit 1
fi

# Step 1: Run workflow tests
echo -e "\n${BLUE}📋 Step 1: Running workflow tests (lint, unit, component, integration, contract)${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

if ./test-workflow-local.sh; then
    echo -e "${GREEN}✅ Workflow tests passed${NC}"
else
    echo -e "${RED}❌ Workflow tests failed${NC}"
    FAILED_STEPS+=("Workflow tests")
    exit 1
fi

# Step 2: Start app without authentication (TestMode enabled)
echo -e "\n${BLUE}📋 Step 2: Starting application without authentication${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

if ! start_app "true" "No Authentication (TestMode enabled)"; then
    FAILED_STEPS+=("Start app (no auth)")
    exit 1
fi

# Step 3: Run E2E smoke and workflow tests
echo -e "\n${BLUE}📋 Step 3: Running E2E tests (Smoke and Workflow)${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

SMOKE_FAILED=false
WORKFLOW_FAILED=false

if ! run_tests "Category=E2E-Smoke" "E2E-Smoke"; then
    SMOKE_FAILED=true
fi

if ! run_tests "Category=E2E-Workflow" "E2E-Workflow"; then
    WORKFLOW_FAILED=true
fi

# Step 4: Stop app
stop_app

# Step 5: Start app with authentication (TestMode disabled)
echo -e "\n${BLUE}📋 Step 4: Starting application with authentication${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

if ! start_app "false" "With Authentication (TestMode disabled)"; then
    FAILED_STEPS+=("Start app (with auth)")
    exit 1
fi

# Step 6: Run E2E auth tests
echo -e "\n${BLUE}📋 Step 5: Running E2E Auth tests${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

AUTH_FAILED=false
if ! run_tests "Category=E2E-Auth" "E2E-Auth"; then
    AUTH_FAILED=true
fi

# Step 7: Stop app
stop_app

# Final summary
echo -e "\n${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Test Summary${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

if [ ${#FAILED_STEPS[@]} -eq 0 ]; then
    echo -e "${GREEN}✅ All tests passed!${NC}"
    echo -e "${GREEN}✅ You can safely push your changes to the repository${NC}"
    echo -e "\n${BLUE}Test results are available in: $TEST_RESULTS_DIR${NC}"
    exit 0
else
    echo -e "${RED}❌ Some tests failed:${NC}"
    for step in "${FAILED_STEPS[@]}"; do
        echo -e "${RED}  - $step${NC}"
    done
    echo -e "\n${YELLOW}⚠️  Please fix the failing tests before pushing${NC}"
    echo -e "${YELLOW}⚠️  DO NOT PUSH changes until all tests pass${NC}"
    echo -e "\n${BLUE}Test results are available in: $TEST_RESULTS_DIR${NC}"
    exit 1
fi

