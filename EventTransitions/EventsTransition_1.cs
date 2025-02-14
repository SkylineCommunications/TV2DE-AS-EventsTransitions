/*
****************************************************************************
*  Copyright (c) 2025,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

05/02/2025	1.0.0.1		NVC, Skyline	Initial version
****************************************************************************
*/

namespace EventsTransition_1
{
    using System;
    using System.Linq;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;
    using Skyline.DataMiner.Net.Sections;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
    public class Script
    {
        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(IEngine engine)
        {
            try
            {
                RunSafe(engine);
            }
            catch (Exception e)
            {
                engine.ExitFail("Run|Something went wrong: " + e);
            }
        }

        private static void RunSafe(IEngine engine)
        {
            DomHelper domHelper = new DomHelper(engine.SendSLNetMessages, "job_event_management");

            ChangeStatesEvents(engine, domHelper);
            ChangeStatesTransmissions(engine, domHelper);
        }

        private static void ChangeStatesTransmissions(IEngine engine, DomHelper domHelper)
        {
            // Get all instances for Transmissions
            var domDefinitionId = Guid.Parse("de0b9904-e66b-43cc-b919-f883a91126ba"); // Transmissions DOM ID
            var definitionFilter = DomInstanceExposers.DomDefinitionId.Equal(domDefinitionId);
            var allForTransmissions = domHelper.DomInstances.Read(definitionFilter);

            if (allForTransmissions == null || !allForTransmissions.Any())
            {
                engine.ExitFail("There are no Instances created for Transmissions.");
                return;
            }

            var transmissionInfoSectionDefinition = domHelper.SectionDefinitions.Read(
                SectionDefinitionExposers.Name.Equal("Transmission Info")).FirstOrDefault();

            if (transmissionInfoSectionDefinition == null)
            {
                engine.ExitFail("Transmission Info section definition not found.");
                return;
            }

            var fieldDescriptors = transmissionInfoSectionDefinition.GetAllFieldDescriptors().ToList();

            if (fieldDescriptors == null || !fieldDescriptors.Any())
            {
                engine.ExitFail("No field descriptors found in Transmission Info section.");
                return;
            }

            var startDateTimeFieldDesc = fieldDescriptors?.FirstOrDefault(start => start.Name == "Start Date");
            var endDateTimeFieldDesc = fieldDescriptors?.FirstOrDefault(end => end.Name == "End Date");

            foreach (var instance in allForTransmissions)
            {
                var dateTimeStart = instance.GetFieldValue<DateTime>(transmissionInfoSectionDefinition, startDateTimeFieldDesc);
                var dateTimeEnd = instance.GetFieldValue<DateTime>(transmissionInfoSectionDefinition, endDateTimeFieldDesc);

                ParseTimeChangeState(dateTimeStart, dateTimeEnd, engine, instance, domHelper);
            }
        }

        private static void ChangeStatesEvents(IEngine engine, DomHelper domHelper)
        {
            // Get all instances for Events
            var domDefinitionId = Guid.Parse("7dcc7992-c5c4-4a39-a61c-812fb31eec60"); // Events DOM ID
            var definitionFilter = DomInstanceExposers.DomDefinitionId.Equal(domDefinitionId);
            var allForEvents = domHelper.DomInstances.Read(definitionFilter);

            if (allForEvents == null || !allForEvents.Any())
            {
                engine.ExitFail("There are no Instances created for Events.");
                return;
            }

            var eventInfoSectionDefinition = domHelper.SectionDefinitions.Read(
                SectionDefinitionExposers.Name.Equal("Event Info")).FirstOrDefault();

            if (eventInfoSectionDefinition == null)
            {
                engine.ExitFail("Event Info section definition not found.");
                return;
            }

            var fieldDescriptors = eventInfoSectionDefinition.GetAllFieldDescriptors().ToList();

            if (fieldDescriptors == null || !fieldDescriptors.Any())
            {
                engine.ExitFail("No field descriptors found in Event Info section.");
                return;
            }

            var startDateTimeFieldDesc = fieldDescriptors?.FirstOrDefault(start => start.Name == "Start Date");
            var endDateTimeFieldDesc = fieldDescriptors?.FirstOrDefault(end => end.Name == "End Date");

            foreach (var instance in allForEvents)
            {
                var dateTimeStart = instance.GetFieldValue<DateTime>(eventInfoSectionDefinition, startDateTimeFieldDesc);
                var dateTimeEnd = instance.GetFieldValue<DateTime>(eventInfoSectionDefinition, endDateTimeFieldDesc);

                ParseTimeChangeState(dateTimeStart, dateTimeEnd, engine, instance, domHelper);
            }
        }

        private static void ParseTimeChangeState(ValueWrapper<DateTime> dateTimeStart, ValueWrapper<DateTime> dateTimeEnd, IEngine engine, DomInstance instance, DomHelper domHelper)
        {
            if (dateTimeStart == null || dateTimeEnd == null || instance == null || engine == null)
            {
                throw new ArgumentNullException("One or more required parameters are null.");
            }

            try
            {
                DateTime dt1 = dateTimeStart.Value;
                DateTime dt2 = dateTimeEnd.Value;
                var currentTime = DateTime.Now;

                if (dt1 >= dt2)
                {
                    throw new ArgumentException("Start time must be earlier than End time.");
                }

                string transitionId = null;

                TimeSpan timeUntilStart = dt1 - currentTime;
                TimeSpan timeSinceEnd = currentTime - dt2;

                bool isStartingSoon = timeUntilStart.TotalMinutes > 0 && timeUntilStart.TotalMinutes <= 5;
                bool isFinishedGracePeriod = timeSinceEnd.TotalMinutes >= 5;

                if (currentTime < dt1 && !isStartingSoon)
                {
                    // Event is still upcoming (No transition needed)
                    return;
                }
                else if ((currentTime >= dt1 && currentTime < dt2) || isStartingSoon)
                {
                    // Event is ongoing
                    transitionId = "upcoming_to_ongoing";
                }
                else if (isFinishedGracePeriod)
                {
                    // Event has finished
                    transitionId = "ongoing_to_finished";
                }

                if (transitionId != null)
                {
                    domHelper.DomInstances.DoStatusTransition(instance.ID, transitionId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ParseTimeChangeState: {ex.Message}");
            }
        }
    }
}
