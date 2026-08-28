# Method and Member Organization

## Class Member Order

Classes must follow this organization order (top to bottom):

1. **Properties, Variables, Getters/Setters** (by access level: public → protected → private)
2. **Constructors**
3. **Public Methods**
4. **Protected Methods**
5. **Private Methods**

This applies to all languages (C#, TypeScript, etc.).

### Variable/Property Access Level Order

Within the properties/variables section, order by access level:

1. **Public** properties, variables, getters/setters
2. **Protected** properties, variables, getters/setters
3. **Private** properties, variables, getters/setters

### Example (TypeScript):

```typescript
export class MyComponent {
  // Properties/Variables (public → protected → private)
  public value: string = '';
  protected count: number = 0;
  private internalState: boolean = false;

  // Getters/Setters (public → protected → private)
  public get data(): string {
    return this.value;
  }

  public set data(val: string) {
    this.value = val;
  }

  protected get internalValue(): string {
    return this.value.toLowerCase();
  }

  private get state(): boolean {
    return this.internalState;
  }

  // Constructors
  constructor() { }

  // Public Methods
  public getData(): string {
    return this.data;
  }

  public setValue(val: string): void {
    this.value = val;
  }

  // Protected Methods
  protected getCount(): number {
    return this.count;
  }

  // Private Methods
  private updateState(): void {
    this.internalState = true;
  }

  private helper(): void {
    // helper logic
  }
}
```

### Example (C#):

```csharp
public class MyClass {
  // Properties/Variables (public → protected → private)
  public string Value { get; set; } = "";
  protected int Count { get; set; } = 0;
  private bool _internalState = false;

  // Constructors
  public MyClass() { }

  // Public Methods
  public string GetData() {
    return Value;
  }

  public void SetValue(string val) {
    Value = val;
  }

  // Protected Methods
  protected int GetCount() {
    return Count;
  }

  // Private Methods
  private void UpdateState() {
    _internalState = true;
  }

  private void Helper() {
    // helper logic
  }
}
```

**Rationale**: This consistent organization makes classes predictable and easy to navigate. Variables and properties at the top immediately show the class state, constructors come next, then the public interface, followed by implementation details.
