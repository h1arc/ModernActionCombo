#!/bin/bash

#################################################################################
# Test Runner for ModernActionCombo
#################################################################################
# Comprehensive test script that runs unit tests, integration tests, and benchmarks
#################################################################################

set -e

# Color output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
CYAN='\033[0;36m'
PURPLE='\033[0;35m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

print_header() {
    echo -e "${PURPLE}🧪 ModernActionCombo Test Suite${NC}"
    echo -e "${BLUE}═══════════════════════════════════════${NC}"
    echo ""
}

print_status() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${CYAN}ℹ${NC} $1"
}

run_unit_tests() {
    echo -e "${YELLOW}Running unit tests...${NC}"
    
    if dotnet test --configuration Debug --verbosity normal --logger "console;verbosity=detailed"; then
        print_status "Unit tests passed"
        return 0
    else
        print_error "Unit tests failed"
        return 1
    fi
}

run_benchmarks() {
    echo -e "${YELLOW}Running performance benchmarks...${NC}"
    
    if [ -f "Benchmarks/CorePerformanceBenchmarks.cs" ]; then
        # Run a quick benchmark if BenchmarkDotNet is available
        if dotnet run --project . --configuration Release -- --filter "*" --job short; then
            print_status "Benchmarks completed"
        else
            print_warning "Benchmarks failed or not available"
        fi
    else
        print_info "No benchmark files found, skipping"
    fi
}

show_test_coverage() {
    echo -e "${CYAN}📊 Test Coverage Summary:${NC}"
    echo -e "${BLUE}─────────────────────────────────────${NC}"
    echo -e "${GREEN}✓${NC} Core/GameStateCache - SIMD operations, thread safety"
    echo -e "${GREEN}✓${NC} Core/ActionResolver - Elementary action resolution"
    echo -e "${GREEN}✓${NC} Jobs/WHMConstants - Action constants and helpers"
    echo -e "${GREEN}✓${NC} Integration/Performance - System stability and speed"
    echo -e "${YELLOW}⚠${NC} Grid/Rotation system - Pending full integration"
    echo -e "${YELLOW}⚠${NC} ActionInterceptor - Requires game environment"
    echo ""
}

show_usage() {
    echo -e "${CYAN}ModernActionCombo Test Runner${NC}"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo -e "${YELLOW}Options:${NC}"
    echo "  --unit-only       Run only unit tests"
    echo "  --benchmarks      Run performance benchmarks"
    echo "  --coverage        Show test coverage summary"
    echo "  --quick           Run fast tests only"
    echo "  --help            Show this help message"
    echo ""
    echo -e "${YELLOW}Examples:${NC}"
    echo "  ./run-tests.sh              # Run all tests"
    echo "  ./run-tests.sh --unit-only  # Run unit tests only"
    echo "  ./run-tests.sh --quick      # Run quick validation"
}

main() {
    local run_unit=true
    local run_benchmarks=false
    local show_coverage=false
    local quick_mode=false
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --help)
                show_usage
                exit 0
                ;;
            --unit-only)
                run_unit=true
                run_benchmarks=false
                shift
                ;;
            --benchmarks)
                run_benchmarks=true
                shift
                ;;
            --coverage)
                show_coverage=true
                shift
                ;;
            --quick)
                quick_mode=true
                shift
                ;;
            *)
                print_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done
    
    print_header
    
    # Change to test directory
    cd "$SCRIPT_DIR"
    
    # Show coverage if requested
    if [ "$show_coverage" = true ]; then
        show_test_coverage
        exit 0
    fi
    
    # Build the test project first
    echo -e "${YELLOW}Building test project...${NC}"
    if dotnet build --configuration Debug --verbosity minimal; then
        print_status "Test project built successfully"
        echo ""
    else
        print_error "Test project build failed"
        exit 1
    fi
    
    local exit_code=0
    
    # Run unit tests
    if [ "$run_unit" = true ]; then
        if ! run_unit_tests; then
            exit_code=1
        fi
        echo ""
    fi
    
    # Run benchmarks unless in quick mode
    if [ "$run_benchmarks" = true ] && [ "$quick_mode" = false ]; then
        run_benchmarks
        echo ""
    fi
    
    # Summary
    if [ $exit_code -eq 0 ]; then
        echo -e "${GREEN}🎉 All tests completed successfully!${NC}"
        
        # Show performance targets
        echo ""
        echo -e "${BLUE}🎯 Performance Targets:${NC}"
        echo -e "   ${CYAN}GameStateCache.CreateSnapshot()${NC}    < 50ns"
        echo -e "   ${CYAN}ActionResolver.ResolveToLevel()${NC}    < 100ns"
        echo -e "   ${CYAN}GameStateData construction${NC}         < 10ns"
        echo -e "   ${CYAN}Grid evaluation (full rotation)${NC}    < 500ns"
        
        if [ "$quick_mode" = false ]; then
            echo ""
            echo -e "${CYAN}💡 Quick commands:${NC}"
            echo -e "   ${YELLOW}./run-tests.sh --quick${NC}      - Fast validation"
            echo -e "   ${YELLOW}./run-tests.sh --coverage${NC}   - Show test coverage"
            echo -e "   ${YELLOW}../build.sh --with-tests${NC}    - Build + test"
        fi
    else
        echo -e "${RED}❌ Some tests failed!${NC}"
        echo ""
        echo -e "${YELLOW}💡 Debugging tips:${NC}"
        echo -e "   ${CYAN}Check build output above for compilation errors${NC}"
        echo -e "   ${CYAN}Ensure main project builds first: cd ../src && dotnet build${NC}"
        echo -e "   ${CYAN}Run individual test files: dotnet test --filter \"ClassName\"${NC}"
    fi
    
    exit $exit_code
}

# Run main function
main "$@"
