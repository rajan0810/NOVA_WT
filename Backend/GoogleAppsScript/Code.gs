/**
 * Google Apps Script backend for the NOVA_WT engine Q&A feature.
 *
 * Replaces the previous (now inaccessible) Apps Script. Receives a question from the
 * Unity app (Assets/Scripts/APIManager.cs), sends it to OpenAI's Chat Completions API,
 * and returns a plain-text answer. No client-side changes are needed - APIManager
 * already POSTs a "parameter" form field and reads the response body as plain text.
 *
 * SETUP:
 * 1. Go to https://script.google.com and create a new project.
 * 2. Delete the default empty function and paste this whole file in.
 * 3. Store your OpenAI key securely (NOT hardcoded in this file):
 *      Project Settings (gear icon, left sidebar) -> Script Properties -> Add script property
 *      Name:  OPENAI_API_KEY
 *      Value: <your OpenAI API key>
 * 4. Deploy -> New deployment -> Type: Web app
 *      Execute as: Me
 *      Who has access: Anyone
 *    Deploy, then copy the resulting /exec URL.
 * 5. In Unity, open QRScanner 1.unity, select the APIManager component, and paste that
 *    URL into the "Gas Url" field, replacing the old one.
 */

// Adjust this to change how the assistant behaves. Keep it short - the answer gets
// read aloud via TTS, so long rambling responses make for a bad voice experience.
const SYSTEM_PROMPT =
  'You are a helpful assistant answering questions about a mechanical engine that the ' +
  'user is exploring in augmented reality by grabbing and inspecting individual parts ' +
  '(such as the crankcase, rocker arm assembly, and other components). Keep answers ' +
  'very short - 1 to 2 short sentences, no more than about 35 words total - since they ' +
  'will be read aloud via text-to-speech and need to finish quickly. Avoid markdown ' +
  'formatting, bullet points, or special characters, since those do not read naturally ' +
  'when spoken.';

// Change this if you want a different model. gpt-4o-mini is a good default: fast and
// inexpensive, well suited to short Q&A like this.
const OPENAI_MODEL = 'gpt-4o-mini';

function doPost(e) {
  try {
    const question = e && e.parameter && e.parameter.parameter;

    if (!question || !question.trim()) {
      return ContentService.createTextOutput('I did not receive a question to answer.');
    }

    const apiKey = PropertiesService.getScriptProperties().getProperty('OPENAI_API_KEY');
    if (!apiKey) {
      return ContentService.createTextOutput(
        'Server is not configured with an OpenAI API key yet.'
      );
    }

    const payload = {
      model: OPENAI_MODEL,
      messages: [
        { role: 'system', content: SYSTEM_PROMPT },
        { role: 'user', content: question }
      ],
      max_tokens: 200,
      temperature: 0.7
    };

    const response = UrlFetchApp.fetch('https://api.openai.com/v1/chat/completions', {
      method: 'post',
      contentType: 'application/json',
      headers: {
        Authorization: 'Bearer ' + apiKey
      },
      payload: JSON.stringify(payload),
      muteHttpExceptions: true
    });

    const statusCode = response.getResponseCode();
    const body = JSON.parse(response.getContentText());

    if (statusCode !== 200) {
      const errorMessage = (body.error && body.error.message) || 'Unknown error from OpenAI.';
      Logger.log('OpenAI error (%s): %s', statusCode, errorMessage);
      return ContentService.createTextOutput('Sorry, I ran into a problem answering that.');
    }

    const answer = body.choices && body.choices[0] && body.choices[0].message
      ? body.choices[0].message.content.trim()
      : 'Sorry, I did not get a usable answer.';

    return ContentService.createTextOutput(answer);
  } catch (err) {
    Logger.log('doPost error: %s', err);
    return ContentService.createTextOutput('Sorry, something went wrong answering that.');
  }
}

/**
 * Lets you sanity-check the deployment URL directly in a browser (GET request) without
 * needing to test from the headset. Visiting the /exec URL should show this message.
 */
function doGet(e) {
  return ContentService.createTextOutput(
    'This endpoint expects POST requests from the Unity app, not GET.'
  );
}
