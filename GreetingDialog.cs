// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// Demonstrates the following concepts:
    /// - Use a subclass of ComponentDialog to implement a multi-turn conversation
    /// - Use a Waterflow dialog to model multi-turn conversation flow
    /// - Use custom prompts to validate user input
    /// - Store conversation and user state.
    /// </summary>
    public class GreetingDialog : ComponentDialog
    {
        // User state for greeting dialog
        private const string GreetingStateProperty = "greetingState";
        private const string EnquiryValue = "greetingEnquiry";
        private const string FlightNumberValue = "greetingFlightNumber";

        // Prompts names
        private const string EnquiryTypePrompt = "enquiryPrompt";
        private const string FlightNumberPrompt = "flightnumberPrompt";

        // Minimum length requirements for enquiry and flight number
        private const int EnquiryTypeLengthMinValue = 3;
        private const int FlightNumberLengthMinValue = 4;

        // Dialog IDs
        private const string ProfileDialog = "profileDialog";

        /// <summary>
        /// Initializes a new instance of the <see cref="GreetingDialog"/> class.
        /// </summary>
        /// <param name="botServices">Connected services used in processing.</param>
        /// <param name="botState">The <see cref="UserState"/> for storing properties at user-scope.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> that enables logging and tracing.</param>
        public GreetingDialog(IStatePropertyAccessor<EnquiryState> userProfileStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(GreetingDialog))
        {
            UserProfileAccessor = userProfileStateAccessor ?? throw new ArgumentNullException(nameof(userProfileStateAccessor));

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                    InitializeStateStepAsync,
                    PromptForEnquiryTypeStepAsync,
                    PromptForFlightNumberStepAsync,
                    DisplayGreetingStateStepAsync,
            };
            AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
            AddDialog(new TextPrompt(EnquiryTypePrompt, ValidateName));
            AddDialog(new TextPrompt(FlightNumberPrompt, ValidateCity));
        }

        public IStatePropertyAccessor<EnquiryState> UserProfileAccessor { get; }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var greetingState = await UserProfileAccessor.GetAsync(stepContext.Context, () => null);
            if (greetingState == null)
            {
                var greetingStateOpt = stepContext.Options as EnquiryState;
                if (greetingStateOpt != null)
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, greetingStateOpt);
                }
                else
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, new EnquiryState());
                }
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> PromptForEnquiryTypeStepAsync(
                                                WaterfallStepContext stepContext,
                                                CancellationToken cancellationToken)
        {
            var greetingState = await UserProfileAccessor.GetAsync(stepContext.Context);

            // if we have everything we need, greet user and return.
            if (greetingState != null && !string.IsNullOrWhiteSpace(greetingState.EnquiryType) && !string.IsNullOrWhiteSpace(greetingState.FlightNumber))
            {
                return await GreetUser(stepContext);
            }

            if (string.IsNullOrWhiteSpace(greetingState.EnquiryType))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "What is your Enquiry Type?",
                    },
                };
                return await stepContext.PromptAsync(EnquiryTypePrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> PromptForFlightNumberStepAsync(
                                                        WaterfallStepContext stepContext,
                                                        CancellationToken cancellationToken)
        {
            // Save name, if prompted.
            var greetingState = await UserProfileAccessor.GetAsync(stepContext.Context);
            var lowerCaseName = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(greetingState.EnquiryType) && lowerCaseName != null)
            {
                // Capitalize and set name.
                greetingState.EnquiryType = char.ToUpper(lowerCaseName[0]) + lowerCaseName.Substring(1);
                await UserProfileAccessor.SetAsync(stepContext.Context, greetingState);
            }

            if (string.IsNullOrWhiteSpace(greetingState.FlightNumber))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"For {greetingState.EnquiryType}, what is your flight number?",
                    },
                };
                return await stepContext.PromptAsync(FlightNumberPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> DisplayGreetingStateStepAsync(
                                                    WaterfallStepContext stepContext,
                                                    CancellationToken cancellationToken)
        {
            // Save city, if prompted.
            var greetingState = await UserProfileAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(greetingState.FlightNumber) &&
                !string.IsNullOrWhiteSpace(lowerCaseCity))
            {
                // capitalize and set city
                greetingState.FlightNumber = char.ToUpper(lowerCaseCity[0]) + lowerCaseCity.Substring(1);
                await UserProfileAccessor.SetAsync(stepContext.Context, greetingState);
            }

            return await GreetUser(stepContext);
        }

        /// <summary>
        /// Validator function to verify if the user name meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateName(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum length for their name.
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= EnquiryTypeLengthMinValue)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Enquiry type needs to be at least `{EnquiryTypeLengthMinValue}` characters long.");
                return false;
            }
        }

        /// <summary>
        /// Validator function to verify if city meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateCity(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum lenght for their name
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= FlightNumberLengthMinValue)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Flight number needs to be at least `{FlightNumberLengthMinValue}` characters long.");
                return false;
            }
        }

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> GreetUser(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var greetingState = await UserProfileAccessor.GetAsync(context);

            // Display their profile information and end dialog.
            await context.SendActivityAsync($"Hi For Enquiry type: {greetingState.EnquiryType}, for flight Number: {greetingState.FlightNumber} is as follows");
            return await stepContext.EndDialogAsync();
        }
    }
}
