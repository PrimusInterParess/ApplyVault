# ApplyVault — CV structuring

ApplyVault turns a CV document—whether started blank or imported from PDF—into an editable Structured CV the user can refine and export.

## Language

**Structured CV**:
The canonical, sectioned representation of a person's CV stored against a CV document—not the raw PDF bytes.
_Avoid_: Parsed CV, JSON CV

**Blank CV**:
A Structured CV created without PDF import, from starter Sections the user fills in.
_Avoid_: Empty CV, new CV from scratch, template CV (when meaning the structured content, not the export layout)

**Starter Entry**:
An Entry shaped for filling (including labeled Contact field slots with empty values)—not sample prose saved as content.
_Avoid_: Placeholder entry, demo entry, sample entry

**Section**:
A titled block in a structured CV (for example Experience or Education), kept in display order.
_Avoid_: Block, chunk

**Entry**:
One logical item inside a section (one role, one degree, one skill group).
_Avoid_: Row, item (when meaning entry)

**Section type**:
The kind of section, which determines which fields each entry may carry—not merely the heading text from the PDF.
_Avoid_: Category, normalized key

**Typed entry fields**:
The field set allowed for entries in a given section type (for example role + employer + dates for Experience).
_Avoid_: Columns, properties bag

**Section schema catalog**:
The single versioned definition of section types and their entry fields, maintained as declarative data in the repository—not duplicated in prompts or UI code.
_Avoid_: Hardcoded section rules, prompt-only schema
