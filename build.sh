#!/bin/bash

#################################################################################
# ModernWrathCombo - Build Script
#################################################################################
# Simple build script that:
# 1. Increments the build number (fourth digit in version)
# 2. Performs a clean build of the plugin
#################################################################################

set -e

# Color output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/ModernWrathCombo.csproj"
MANIFEST_FILE="$SCRIPT_DIR/src/ModernWrathCombo.json"

print_header() {
    echo -e "${BLUE}🚀 Building ModernWrathCombo...${NC}"
    echo ""
}

print_status() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

increment_version() {
    echo -e "${YELLOW}Incrementing build version...${NC}"
    
    # Extract current version from csproj
    local current_assembly_version=$(grep -o '<AssemblyVersion>[^<]*' "$PROJECT_FILE" | sed 's/<AssemblyVersion>//')
    local current_file_version=$(grep -o '<FileVersion>[^<]*' "$PROJECT_FILE" | sed 's/<FileVersion>//')
    
    if [ -z "$current_assembly_version" ]; then
        # If no version exists, create initial version tags
        echo -e "${YELLOW}No version found, adding initial version 1.0.0.1...${NC}"
        
        # Add version properties to project file before </PropertyGroup>
        if [[ "$OSTYPE" == "darwin"* ]]; then
            # macOS
            sed -i '' 's|</PropertyGroup>|  <AssemblyVersion>1.0.0.1</AssemblyVersion>\
  <FileVersion>1.0.0.1</FileVersion>\
</PropertyGroup>|' "$PROJECT_FILE"
        else
            # Linux
            sed -i 's|</PropertyGroup>|  <AssemblyVersion>1.0.0.1</AssemblyVersion>\n  <FileVersion>1.0.0.1</FileVersion>\n</PropertyGroup>|' "$PROJECT_FILE"
        fi
        
        print_status "Version initialized: 1.0.0.1"
        update_manifest_version "1.0.0.1"
        return 0
    fi
    
    # Split version into parts (major.minor.patch.build)
    IFS='.' read -ra VERSION_PARTS <<< "$current_assembly_version"
    
    if [ ${#VERSION_PARTS[@]} -ne 4 ]; then
        print_error "Version format should be major.minor.patch.build (e.g., 1.0.0.0)"
        exit 1
    fi
    
    # Increment build number (fourth digit)
    local major="${VERSION_PARTS[0]}"
    local minor="${VERSION_PARTS[1]}" 
    local patch="${VERSION_PARTS[2]}"
    local build="${VERSION_PARTS[3]}"
    
    build=$((build + 1))
    
    local new_version="$major.$minor.$patch.$build"
    
    # Update AssemblyVersion and FileVersion
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        sed -i '' "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$new_version</AssemblyVersion>|" "$PROJECT_FILE"
        sed -i '' "s|<FileVersion>.*</FileVersion>|<FileVersion>$new_version</FileVersion>|" "$PROJECT_FILE"
    else
        # Linux
        sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$new_version</AssemblyVersion>|" "$PROJECT_FILE"
        sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>$new_version</FileVersion>|" "$PROJECT_FILE"
    fi
    
    print_status "Version updated: $current_assembly_version → $new_version"
    
    # Update manifest version to match
    update_manifest_version "$new_version"
}

update_manifest_version() {
    local new_version="$1"
    
    if [ -f "$MANIFEST_FILE" ]; then
        if [[ "$OSTYPE" == "darwin"* ]]; then
            # macOS
            sed -i '' "s|\"AssemblyVersion\": \"[^\"]*\"|\"AssemblyVersion\": \"$new_version\"|" "$MANIFEST_FILE"
        else
            # Linux
            sed -i "s|\"AssemblyVersion\": \"[^\"]*\"|\"AssemblyVersion\": \"$new_version\"|" "$MANIFEST_FILE"
        fi
        print_status "Manifest version synced: $new_version"
    else
        echo -e "${YELLOW}No manifest file found, skipping manifest update${NC}"
    fi
}

build_project() {
    echo -e "${YELLOW}Cleaning previous builds...${NC}"
    dotnet clean src/ --verbosity quiet
    print_status "Clean completed"
    
    echo ""
    echo -e "${YELLOW}Building project...${NC}"
    if dotnet build src/ --configuration Debug --verbosity minimal --no-restore; then
        echo ""
        print_status "Build completed successfully!"
        
        # Show build info
        local dll_path="$SCRIPT_DIR/src/bin/Debug/net9.0-windows/ModernWrathCombo.dll"
        if [ -f "$dll_path" ]; then
            local dll_size=$(du -h "$dll_path" | cut -f1)
            echo -e "${BLUE}📦 Output: $dll_path ($dll_size)${NC}"
        fi
        
        # Show current version
        local current_version=$(grep -o '<AssemblyVersion>[^<]*' "$PROJECT_FILE" | sed 's/<AssemblyVersion>//')
        echo -e "${BLUE}🏷️  Version: $current_version${NC}"
        
        return 0
    else
        echo ""
        print_error "Build failed!"
        return 1
    fi
}

run_tests() {
    if [ "$1" = "--with-tests" ]; then
        echo ""
        echo -e "${YELLOW}Running quick performance validation...${NC}"
        if [ -f "$SCRIPT_DIR/tests/run-benchmarks.sh" ]; then
            cd "$SCRIPT_DIR/tests"
            if ./run-benchmarks.sh quick; then
                print_status "Performance validation passed"
            else
                print_error "Performance validation failed"
                return 1
            fi
            cd "$SCRIPT_DIR"
        else
            echo -e "${YELLOW}No benchmark script found, skipping tests${NC}"
        fi
    fi
}

show_usage() {
    echo "Usage: $0 [--with-tests]"
    echo ""
    echo "Options:"
    echo "  --with-tests    Run quick performance validation after build"
    echo "  --help         Show this help message"
    echo ""
    echo "This script will:"
    echo "  1. Increment the build number (4th digit of version)"
    echo "  2. Clean and build the ModernWrathCombo plugin"
    echo "  3. Optionally run performance tests"
}

main() {
    if [ "$1" = "--help" ]; then
        show_usage
        exit 0
    fi
    
    print_header
    
    # Change to script directory
    cd "$SCRIPT_DIR"
    
    # Increment version
    increment_version
    echo ""
    
    # Restore packages quietly
    echo -e "${YELLOW}Restoring packages...${NC}"
    dotnet restore src/ --verbosity quiet
    print_status "Package restore completed"
    echo ""
    
    # Build project
    if build_project; then
        # Run tests if requested
        if run_tests "$1"; then
            echo ""
            echo -e "${GREEN}🎉 Build completed successfully!${NC}"
            
            # Show quick usage reminder
            echo ""
            echo -e "${BLUE}💡 Quick commands:${NC}"
            echo -e "   ${YELLOW}./build.sh${NC}              - Build with version increment"
            echo -e "   ${YELLOW}./build.sh --with-tests${NC} - Build + run performance tests"
            echo -e "   ${YELLOW}./tests/run-benchmarks.sh${NC} - Run full benchmark suite"
            exit 0
        else
            exit 1
        fi
    else
        exit 1
    fi
}

# Run main function
main "$@"
