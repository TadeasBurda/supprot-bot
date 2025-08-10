# 🧠 Prompt: Order and Format C# Class Members

## 📝 Description
This prompt formats and reorders members of a C# class to follow a clean and consistent structure. It ensures proper indentation and organizes members in a logical order for readability and maintainability.

## 🎯 Goals
- Format code with 4-space indentation.
- Place opening braces on new lines.
- Reorder class members in the following order:
  1. constants (`const`, e.g. `const int MAX_DATA_POINTS`)
  2. abstract fields
  3. private static readonly fields (e.g. static readonly int _count)
  4. private static fields with default value (e.g. static int = 0)
  5. private readonly fields (e.g. readonly string _name)
  6. private fields with default value (e.g. int = 0)
  7. private nullable fields (e.g. int?)
  8. protected static readonly fields
  9. protected static fields
  10. protected readonly fields
  11. protected fields
  12. abstract properties
  13. internal properties
  14. public properties
  15. properties with the `[ObservableProperty]` attribute
  16. Constructors
  17. abstract methods (including async)
  18. Methods (in order: public, protected, private; async methods are placed after non-async in each group)
  19. `public void Dispose()` method (always at the end, before the destructor)
  20. Destructor (`~Class()`, always the very last member)
- Constants must always be placed first and use UPPER_SNAKE_CASE naming convention (e.g. `MAX_DATA_POINTS`).
- Abstract fields, properties, and methods must always be placed before non-abstract members of the same type.
- Properties with the `[ObservableProperty]` attribute must always be grouped together, immediately after normal properties.
- The `public void Dispose()` method must always be placed at the end of the class, just before the destructor.
- The destructor (`~Class()`) must always be the very last member of the class.
- If a constructor is a primary constructor, do not change it to a regular constructor; always preserve its primary form.
- Normalize naming conventions (PascalCase for types and members, camelCase for fields).
- Remove unnecessary whitespace and align code blocks.

## 📥 Input
A C# class file with unordered or poorly formatted members.

## 📤 Output
A clean, well-formatted C# class with members ordered and styled according to standard conventions.

## 🛠️ Notes
- Do not modify logic or add/remove comments unless necessary for formatting.
- Preserve attributes, region tags, and preprocessor directives.
- **Remove all helper comments used for member grouping (e.g. `// 1. Constants`, `// (none)`) from the final output. These comments must not remain in the resulting code.**
	