#!/bin/bash

#################################################################################
# ModernActionCombo - Enhanced Build Script with Smart Versioning
#################################################################################
# Enhanced build script that:
# 1. Intelligently increments version based on changes
# 2. Updates both project and manifest files
# 3. Provides clear visual feedback
# 4. Supports custom version increments
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
PROJECT_FILE="$SCRIPT_DIR/src/ModernActionCombo.csproj"
MANIFEST_FILE="$SCRIPT_DIR/src/ModernActionCombo.json"
VERSION_LOG="$SCRIPT_DIR/.version_log"

print_header() {
    echo -e "${PURPLE}🚀 ModernActionCombo Build System${NC}"
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

get_current_version() {
    local version=$(grep -o '<AssemblyVersion>[^<]*' "$PROJECT_FILE" | sed 's/<AssemblyVersion>//')
    echo "$version"
}

get_version_type() {
    local version_type="$1"
    
    case "$version_type" in
        "major"|"maj")
            echo "major"
            ;;
        "minor"|"min")
            echo "minor"
            ;;
        "patch"|"pat")
            echo "patch"
            ;;
        "build"|"bld"|"")
            echo "build"
            ;;
        *)
            print_error "Invalid version type: $version_type"
            echo "Valid types: major, minor, patch, build (default)"
            exit 1
            ;;
    esac
}

increment_version() {
    local version_type=$(get_version_type "$1")
    local comment="$2"
    
    # Use default comment if none provided
    if [ -z "$comment" ]; then
        comment="Build increment"
    fi
    
    echo -e "${YELLOW}Incrementing $version_type version...${NC}"
    
    # Extract current version from csproj
    local current_version=$(get_current_version)
    
    if [ -z "$current_version" ]; then
        # If no version exists, create initial version tags
        print_warning "No version found, initializing version 1.0.0.1..."
        
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
        log_version_change "INIT" "1.0.0.1" "Initial version"
        return 0
    fi
    
    # Split version into parts (major.minor.patch.build)
    IFS='.' read -ra VERSION_PARTS <<< "$current_version"
    
    if [ ${#VERSION_PARTS[@]} -ne 4 ]; then
        print_error "Version format should be major.minor.patch.build (e.g., 1.0.0.0)"
        exit 1
    fi
    
    # Parse version components
    local major="${VERSION_PARTS[0]}"
    local minor="${VERSION_PARTS[1]}" 
    local patch="${VERSION_PARTS[2]}"
    local build="${VERSION_PARTS[3]}"
    
    # Increment based on type
    case "$version_type" in
        "major")
            major=$((major + 1))
            minor=0
            patch=0
            build=0
            ;;
        "minor")
            minor=$((minor + 1))
            patch=0
            build=0
            ;;
        "patch")
            patch=$((patch + 1))
            build=0
            ;;
        "build")
            build=$((build + 1))
            ;;
    esac
    
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
    
    print_status "Version updated: ${current_version} → ${new_version} (${version_type})"
    
    # Update manifest version to match
    update_manifest_version "$new_version"
    log_version_change "$version_type" "$new_version" "$comment"
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
        print_warning "No manifest file found, skipping manifest update"
    fi
}

log_version_change() {
    local change_type="$1"
    local new_version="$2"
    local description="$3"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    echo "$timestamp | $change_type | $new_version | $description" >> "$VERSION_LOG"
}

show_version_history() {
    if [ -f "$VERSION_LOG" ]; then
        echo -e "${CYAN}📖 Recent Version History:${NC}"
        echo -e "${BLUE}─────────────────────────────────────${NC}"
        tail -n 5 "$VERSION_LOG" | while IFS='|' read -r timestamp change_type version description; do
            echo -e "${YELLOW}$timestamp${NC} | ${GREEN}$change_type${NC} | ${PURPLE}$version${NC} | $description"
        done
        echo ""
    fi
}

build_project() {
    echo -e "${YELLOW}Saving all files...${NC}"
    # Send save-all command to VS Code if it's running
    if command -v code &> /dev/null; then
        code --command "workbench.action.files.saveAll" 2>/dev/null || true
    fi
    print_status "Files saved"
    
    echo ""
    echo -e "${YELLOW}Cleaning previous builds...${NC}"
    dotnet clean src/ --verbosity quiet
    print_status "Clean completed"
    
    echo ""
    echo -e "${YELLOW}Building project...${NC}"
    if dotnet build src/ --configuration Debug --verbosity minimal --no-restore; then
        echo ""
        print_status "Build completed successfully!"
        
        # Show build info
        local dll_path="$SCRIPT_DIR/src/bin/Debug/net9.0-windows/ModernActionCombo.dll"
        if [ -f "$dll_path" ]; then
            local dll_size=$(du -h "$dll_path" | cut -f1)
            echo -e "${BLUE}📦 Output: ModernActionCombo.dll ($dll_size)${NC}"
        fi
        
        # Show current version prominently
        local current_version=$(get_current_version)
        echo -e "${PURPLE}🏷️  Version: $current_version${NC}"
        
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
        echo -e "${YELLOW}Running quick validation...${NC}"
        if [ -f "$SCRIPT_DIR/tests/run-benchmarks.sh" ]; then
            cd "$SCRIPT_DIR/tests"
            if ./run-benchmarks.sh quick; then
                print_status "Validation passed"
            else
                print_error "Validation failed"
                return 1
            fi
            cd "$SCRIPT_DIR"
        else
            print_info "No test script found, skipping validation"
        fi
    fi
}

show_usage() {
    echo -e "${CYAN}ModernActionCombo Build Script${NC}"
    echo ""
    echo "Usage: $0 [VERSION_TYPE] [OPTIONS]"
    echo ""
    echo -e "${YELLOW}Version Types:${NC}"
    echo "  build, bld     Increment build number (default) - 1.0.0.1 → 1.0.0.2"
    echo "  patch, pat     Increment patch version - 1.0.0.5 → 1.0.1.0"
    echo "  minor, min     Increment minor version - 1.0.3.2 → 1.1.0.0"
    echo "  major, maj     Increment major version - 1.2.1.5 → 2.0.0.0"
    echo ""
    echo -e "${YELLOW}Options:${NC}"
    echo "  --with-tests   Run validation after build"
    echo "  --no-version   Skip version increment"
    echo "  --comment, -m  Add custom description to version log"
    echo "  --history      Show version history"
    echo "  --current      Show current version"
    echo "  --help         Show this help message"
    echo ""
    echo -e "${YELLOW}Examples:${NC}"
    echo "  ./build.sh                 # Quick build (increment build number)"
    echo "  ./build.sh patch           # Increment patch version"
    echo "  ./build.sh minor --with-tests  # Minor version + run tests"
    echo "  ./build.sh patch -m \"Bug fixes\"  # Patch with custom description"
    echo "  ./build.sh --current       # Show current version"
}

main() {
    # Parse arguments
    local version_type=""
    local skip_version=false
    local with_tests=false
    local show_history=false
    local show_current=false
    local custom_comment=""
    
    while [[ $# -gt 0 ]]; do
        case $1 in
            --help)
                show_usage
                exit 0
                ;;
            --no-version)
                skip_version=true
                shift
                ;;
            --with-tests)
                with_tests=true
                shift
                ;;
            --history)
                show_history=true
                shift
                ;;
            --current)
                show_current=true
                shift
                ;;
            --comment|-m)
                if [[ -n "$2" && "$2" != -* ]]; then
                    custom_comment="$2"
                    shift 2
                else
                    print_error "Comment flag requires a message"
                    echo "Usage: $0 [version] --comment \"Your description here\""
                    exit 1
                fi
                ;;
            major|maj|minor|min|patch|pat|build|bld)
                version_type="$1"
                shift
                ;;
            *)
                print_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done
    
    # Handle info requests
    if [ "$show_current" = true ]; then
        local current_version=$(get_current_version)
        echo -e "${PURPLE}Current Version: $current_version${NC}"
        exit 0
    fi
    
    if [ "$show_history" = true ]; then
        show_version_history
        exit 0
    fi
    
    print_header
    
    # Change to script directory
    cd "$SCRIPT_DIR"
    
    # Show version history if available
    show_version_history
    
    # Increment version unless skipped
    if [ "$skip_version" = false ]; then
        increment_version "$version_type" "$custom_comment"
        echo ""
    else
        print_info "Skipping version increment"
        echo ""
    fi
    
    # Restore packages quietly
    echo -e "${YELLOW}Restoring packages...${NC}"
    dotnet restore src/ --verbosity quiet
    print_status "Package restore completed"
    echo ""
    
    # Build project
    if build_project; then
        # Run tests if requested
        if [ "$with_tests" = true ]; then
            if ! run_tests "--with-tests"; then
                exit 1
            fi
        fi
        
        echo ""
        echo -e "${GREEN}🎉 Build completed successfully!${NC}"
        
        # Show quick usage reminder
        echo ""
        echo -e "${BLUE}💡 Quick commands:${NC}"
        echo -e "   ${YELLOW}./build.sh${NC}              - Quick build (increment build)"
        echo -e "   ${YELLOW}./build.sh patch${NC}         - Increment patch version"
        echo -e "   ${YELLOW}./build.sh --current${NC}     - Show current version"
        echo -e "   ${YELLOW}./build.sh --history${NC}     - Show version history"
        exit 0
    else
        exit 1
    fi
}

# Run main function
main "$@"
