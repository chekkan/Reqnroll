# Scenario Context

`ScenarioContext` provides access to several functions, which are demonstrated using the following scenarios.

## Accessing the ScenarioContext

### In Bindings

To access the `ScenarioContext` you have to get it via [context injection](context-injection).

Example:

```{code-block} csharp
:caption: Step Definition File

[Binding]
public class Binding
{
    private ScenarioContext _scenarioContext;

    public Binding(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }
}
```

Now you can access the ScenarioContext in all your step definitions with the `_scenarioContext` field.

### In Hooks

#### Before/AfterTestRun

Accessing the `ScenarioContext` is not possible, as no `Scenario` is executed when the hook is called.

#### Before/AfterFeature

Accessing the `ScenarioContext` is not possible, as no `Scenario` is executed when the hook is called.

#### Before/AfterScenario

Accessing the `ScenarioContext` is done like in [normal bindings](#in-bindings)

#### Before/AfterStep

Accessing the `ScenarioContext` is done like in [normal bindings](#in-bindings)

### Migrating from ScenarioContext.Current

The `ScenarioContext.Current` static accessor is obsolete, to make clear that you that you should avoid using these properties in future. The reason for moving away from these properties is that they do not work when running scenarios in parallel.

So how do you now access ScenarioContext?

Replace the code with `ScenarioContext.Current`...

```{code-block} csharp
:caption: Step Definition File

[Binding]
public class Bindings
{
    [Given(@"I have entered (.*) into the calculator")]
    public void GivenIHaveEnteredIntoTheCalculator(int number)
    {
        ScenarioContext.Current["Number1"] = number;
    }

    [BeforeScenario()]
    public void BeforeScenario()
    {
        Console.WriteLine("Starting " + ScenarioContext.Current.ScenarioInfo.Title);
    }
}
```

...to use [Context-Injection](context-injection) to acquire an instance of ScenarioContext by requesting it via the constructor.


```{code-block} csharp
:caption: Step Definition File

[Binding]
public class Bindings
{
    private readonly ScenarioContext _scenarioContext;

    public Bindings(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [Given(@"I have entered (.*) into the calculator")]
    public void GivenIHaveEnteredIntoTheCalculator(int number)
    {
        _scenarioContext["Number1"] = number;
    }

    [BeforeScenario()]
    public void BeforeScenario()
    {
        Console.WriteLine("Starting " + _scenarioContext.ScenarioInfo.Title);
    }
}
```

## ScenarioContext.Pending

See [](../execution/mark-steps-as-not-implemented.md)

## Storing data in the ScenarioContext

ScenarioContext helps you store values in a dictionary between steps. This helps you to organize your step definitions better than using private variables in step definition classes.

There are some type-safe extension methods that help you to manage the contents of the dictionary in a safer way. To do so, you need to include the namespace Reqnroll.Assist, since these methods are extension methods of ScenarioContext.

## ScenarioContext.ScenarioInfo

`ScenarioContext.ScenarioInfo` allows you to access information about the scenario currently being executed, such as its title and scenario and feature tags:

In the .feature file:

```{code-block} gherkin
:caption: Feature File

@feature_tag
Feature: My feature

@rule_tag
Rule: My rule

@scenario_tag1 @scenario_tag2
Scenario: Showing information of the scenario

When I execute any scenario
Then the ScenarioInfo contains the following information
    | Field         | Value                                               |
    | Title         | Showing information of the scenario                 |
    | Tags          | scenario_tag1, scenario_tag2                        |
    | CombinedTags  | scenario_tag1, scenario_tag2, feature_tag, rule_tag |
```

and in the step definition:

```{code-block} csharp
:caption: Step Definition File

private class ScenarioInformation
{
    public string Title { get; set; }
    public string[] Tags { get; set; }
    public string[] CombinedTags { get; set; }
}

[When(@"I execute any scenario")]
public void ExecuteAnyScenario(){}

[Then(@"the ScenarioInfo contains the following information")]
public void ScenarioInfoContainsInterestingInformation(DataTable table)
{
    // Create our small DTO for the info from the step
    var fromStep = table.CreateInstance<ScenarioInformation>();
    fromStep.Tags =  table.Rows[0]["Value"].Split(',');

    // Short-hand to the scenarioInfo
    var si = _scenarioContext.ScenarioInfo;

    // Assertions
    si.Title.Should().Equal(fromStep.Title);
    si.Tags.Should().BeEquivalentTo(fromStep.Tags);
    si.CombinedTags.Should().BeEquivalentTo(fromStep.CombinedTags);
}
```

`ScenarioContext.ScenarioInfo` also provides access to the current set of arguments from the scenario's examples in the form of an `IOrderedDictionary`: 

```{code-block} gherkin
:caption: Feature File

Scenario: Accessing the current example 

When I use examples in my scenario
Then the examples are available in ScenarioInfo 

    Examples:
    | Sport      | TeamSize |
    | Soccer     | 11       |
    | Basketball | 6        |
```

```{code-block} csharp
:caption: Step Definition File

public class ScenarioExamplesDemo
{
    private ScenarioInfo _scenarioInfo;

    public ScenarioExamplesDemo(ScenarioInfo scenarioInfo)
    {
        _scenarioInfo = scenarioInfo;
    }

    [When(@"I use examples in my scenario")]
    public void IUseExamplesInMyScenario() {}

    [Then(@"the examples are available in ScenarioInfo")]
    public void TheExamplesAreAvailableInScenarioInfo()
    {
        var currentArguments = _scenarioInfo.Arguments;
        var currentSport = currentArguments["Sport"];
        var currentTeamSize = currentArguments["TeamSize"];
        Console.WriteLine($"The current sport is {currentSport}");
        Console.WriteLine($"The current sport allows teams of {currentTeamSize} players");
    }
}
```

Another use is to check if an error has occurred, which is possible with the `ScenarioContext.TestError` property, which simply returns the exception.

You can use this information for “error handling”. Here is an uninteresting example:

In the feature file...

```{code-block} gherkin
:caption: Feature File

#This is not so easy to write a scenario for but I've created an AfterScenario-hook
@showingErrorHandling
Scenario: Display error information in AfterScenario
When an error occurs in a step

```

...and the step definition:

```{code-block} csharp
:caption: Step Definition File

[When("an error occurs in a step")]
public void AnErrorOccurs()
{
    "not correct".Should().Equal("correct");
}

[AfterScenario("showingErrorHandling")]
public void AfterScenarioHook()
{
    if(_scenarioContext.TestError != null)
    {
        var error = _scenarioContext.TestError;
        Console.WriteLine("An error ocurred:" + error.Message);
        Console.WriteLine("It was of type:" + error.GetType().Name);
    }
}
```

This is another example, that might be more useful:

```{code-block} csharp
:caption: Hook File

[AfterScenario]
public void AfterScenario()
{
    if(_scenarioContext.TestError != null)
    {
        WebBrowser.Driver.CaptureScreenShot(_scenarioContext.ScenarioInfo.Title);
    }
}
```

In this case, MvcContrib is used to capture a screenshot of the failing test and name the screenshot after the title of the scenario.

## ScenarioContext.RuleInfo

`ScenarioContext.RuleInfo` allows you to access information about the rule for the scenario currently being executed, such as its title and tags:

In the .feature file:

```{code-block} gherkin
:caption: Feature File

@feature_tag
Feature: My feature

@rule_tag
Rule: My rule

@scenario_tag1 @scenario_tag2
Scenario: Showing information of the scenario

When I execute any scenario
Then the RuleInfo contains the following information
    | Field         | Value    |
    | Title         | My rule  |
    | Tags          | rule_tag |
```

and in the step definition:

```{code-block} csharp
:caption: Step Definition File

private class RuleInformation
{
    public string Title { get; set; }
    public string[] Tags { get; set; }
}

[When(@"I execute any scenario")]
public void ExecuteAnyScenario(){}

[Then(@"the RuleInfo contains the following information")]
public void RuleInfoContainsInterestingInformation(DataTable table)
{
    // Create our small DTO for the info from the step
    var fromStep = table.CreateInstance<RuleInformation>();
    fromStep.Tags =  table.Rows[0]["Value"].Split(',');

    // Short-hand to the ruleInfo
    var ri = _scenarioContext.RuleInfo;

    // Assertions
    ri.Title.Should().Equal(fromStep.Title);
    ri.Tags.Should().BeEquivalentTo(fromStep.Tags);
}
```

`ScenarioContext.RuleInfo` will be null if there is no valid `Rule` in the feature file.

## ScenarioContext.CurrentScenarioBlock

Use `ScenarioContext.CurrentScenarioBlock` to query the “type” of step (Given, When or Then). This can be used to execute additional setup/cleanup code right before or after Given, When or Then blocks.

In the feature file:

```{code-block} gherkin
:caption: Feature File

Scenario: Show the type of step we're currently on
    Given I have a Given step
    And I have another Given step
    When I have a When step
    Then I have a Then step
```

...and the step definition:

```{code-block} csharp
:caption: Step Definition File

[Given("I have a (.*) step")]
[Given("I have another (.*) step")]
[When("I have a (.*) step")]
[Then("I have a (.*) step")]
public void ReportStepTypeName(string expectedStepType)
{
    var stepType = _scenarioContext.CurrentScenarioBlock.ToString();
    stepType.Should().Equal(expectedStepType);
}
```

## ScenarioContext.StepContext

Sometimes you need to access the currently executed step, e.g. to improve tracing. Use the `_scenarioContext.StepContext` property for this purpose.
