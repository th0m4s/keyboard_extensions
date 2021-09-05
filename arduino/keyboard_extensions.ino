#include <EEPROM.h>

#define TYPE_PING 1
#define TYPE_NORMAL 2

#define PING_TIME 500

#define CMD_REQ_SETTINGS 1
#define CMD_RESP_SETTINGS 2
#define CMD_SET_KEY_SETTING 3
#define CMD_REQ_VOICE 4
// resp_voice is also used when updates are received from discord
#define CMD_RESP_VOICE 5
#define CMD_KEY 6
#define CMD_SPECIAL 7
#define CMD_SET_OTHER_SETTING 8

#define VOICE_MUTE 0
#define VOICE_DEAF 1
#define VOICE_ALL 2

#define SETTING_DEBOUNCE 0
#define SETTING_TRIGGER 1

#define TRIGGER_UP 0
#define TRIGGER_DOWN 1

const byte numKeys = 7;
const byte pins[numKeys] = {2, 3, 4, 5, 6, 7, 8};
bool pressed[numKeys];
byte debounces[numKeys];
const byte numSpecial = 2;
const byte specialKeys[numSpecial] = {5, 6};
bool isSpecial[numKeys];
const byte specialLedPins[numSpecial] = {9, 10};

byte debounceCount = 12;
byte specialTrigger = TRIGGER_DOWN;

byte keyModes[numKeys];
byte keyValues[numKeys];

void setup() {
  Serial.begin(19200);

  for(byte i = 0; i < numKeys; i++) {
    byte pin = pins[i];
    pinMode(pin, INPUT_PULLUP);

    pressed[i] = false;
    debounces[i] = 0;
    isSpecial[i] = false;
  }

  for(byte i = 0; i < numSpecial; i++) {
    pinMode(specialLedPins[i], OUTPUT);
    isSpecial[specialKeys[i]] = true;
  }

  pinMode(LED_BUILTIN, OUTPUT);
  loadSave();
}

byte messageType = 0;
byte messageLength = 0;
byte message[255];
byte msgPosition = 0;

long LastTickReceived = -2*PING_TIME;
bool connected = false;

bool discordMute = false;
bool discordDeaf = false;

void loop() {
  long now = millis();
  if(connected && now - LastTickReceived > 2*PING_TIME) {
    connected = false;
    digitalWrite(LED_BUILTIN, LOW);

    discordMute = false;
    discordDeaf = false;
    updateSpecialKeys();
  } else if(!connected && now - LastTickReceived < 2* PING_TIME) {
    connected = true;
    digitalWrite(LED_BUILTIN, HIGH);
    sendCommand(TYPE_NORMAL, 2, new byte[2] {CMD_REQ_VOICE, VOICE_ALL});
  }

  for(byte i = 0; i < numKeys; i++) {
    byte pin = pins[i];
    bool current = pressed[i];
    bool newState = !digitalRead(pin);

    if(current != newState) {
      if(debounces[i]++ >= debounceCount) {
        pressed[i] = newState;
        
        if(isSpecial[i]) {
          if(specialTrigger == newState)
            sendCommand(TYPE_NORMAL, 2, new byte[3] {CMD_SPECIAL, i});
        } else {
          sendCommand(TYPE_NORMAL, 3, new byte[3] {CMD_KEY, i, newState});
        }
      }
    } else {
      debounces[i] = 0;
    }
  }

  if(Serial.available() > 0) {
    if(messageType == 0) {
      messageType = Serial.read();
    }

    if(Serial.available() > 0) {
      if(messageLength == 0) {
        messageLength = Serial.read() + 1;
        msgPosition = 0;
      }

      while(Serial.available() > 0 && msgPosition < messageLength-1) {
        message[msgPosition++] = Serial.read();
      }
    }
  }

  if(messageLength > 0 && messageLength-1 == msgPosition) {
    processCommand(messageType, messageLength, message);

    messageType = 0;
    messageLength = 0;
    msgPosition = 0;
  }
}

void updateSpecialKeys() {
  for(byte i = 0; i < numSpecial; i++) {
    byte key = specialKeys[i];

    byte specialPin = specialLedPins[i];
    if(keyModes[key] == 1) {
      if(keyValues[key] == 0) {
        digitalWrite(specialPin, discordMute);
      } else if(keyValues[key] == 1) {
        digitalWrite(specialPin, discordDeaf);
      } else digitalWrite(specialPin, LOW);
    } else {
      digitalWrite(specialPin, LOW);
    }
  }
}

void processCommand(byte type, byte length, byte message[]) {
  if(type == TYPE_PING) {
    LastTickReceived = millis();
    sendCommand(TYPE_PING, 0, new byte[0]);
  } else if(type == TYPE_NORMAL && length >= 1) {
    byte command = message[0];
    if(command == CMD_REQ_SETTINGS) {
      byte respLength = 1 + 2 + numKeys*2 + numSpecial + 2;
      byte settings[respLength];
      settings[0] = CMD_RESP_SETTINGS;
      settings[1] = numKeys;
      settings[2] = numSpecial;
      for(byte i = 0; i < numSpecial; i++) {
        settings[3 + i] = specialKeys[i];
      }
      for(byte i = 0; i < numKeys; i++) {
        settings[3 + numSpecial + i*2] = keyModes[i];
        settings[4 + numSpecial + i*2] = keyValues[i];
      }

      byte settingBasePos = 3 + numSpecial + numKeys * 2;
      settings[settingBasePos + SETTING_DEBOUNCE] = debounceCount;
      settings[settingBasePos + SETTING_TRIGGER] = specialTrigger;

      sendCommand(TYPE_NORMAL, respLength, settings);
    } else if(command == CMD_SET_KEY_SETTING) {
      byte key = message[1];
      byte setting = message[2];
      byte data = message[3];
      if(setting == 0) { // key mode
        keyModes[key] = data;
      } else if(setting == 1) { // key val
        keyValues[key] = data;
      }

      EEPROM.write(2 + setting + key*2, data);
    } else if(command == CMD_RESP_VOICE) {
      byte voiceType = message[1];
      if(voiceType == VOICE_MUTE) {
        discordMute = message[2] != 0;
      } else if(voiceType == VOICE_DEAF) {
        discordDeaf = message[2] != 0;
      } else if(voiceType == VOICE_ALL) {
        discordMute = message[2] != 0;
        discordDeaf = message[3] != 0;
      }

      updateSpecialKeys();
    } else if(command == CMD_SET_OTHER_SETTING) {
      byte setting = message[1];
      byte value = message[2];

      if(setting == SETTING_DEBOUNCE) {
        debounceCount = value;
      } else if(setting == SETTING_TRIGGER) {
        specialTrigger = value;
      }

      writeSave();
    }
  }
}

void sendCommand(byte type, byte length, byte message[]) {
  Serial.write(type);
  Serial.write(length);
  Serial.write(message, length);
}

void loadDefaults(bool save = true) {
  for(byte i = 0; i < numKeys; i++) {
    keyModes[i] = 0;
    keyValues[i] = 18+i;
  }

  debounceCount = 12;
  specialTrigger = TRIGGER_DOWN;

  if(save) writeSave();
}

void _loadSaveV1() {
  byte _numKeys = EEPROM.read(1);
  bool newSave = _numKeys != numKeys;
  if(newSave) {
    loadDefaults(false);
  }

  for(byte i = 0; i < numKeys; i++) {
    keyModes[i] = EEPROM.read(2 + i*2);
    keyValues[i] = EEPROM.read(3 + i*2);
  }
}

void loadSave() {
  byte saveVersion = EEPROM.read(0);
  if(saveVersion == 1) {
    _loadSaveV1();
    writeSave(); // rewrite save because it is not last version
  } else if(saveVersion == 2) {
    _loadSaveV1(); // v2 = v1 + new settings (debounce + special trigger)
    byte basePos = 2 + numKeys * 2;
    debounceCount = EEPROM.read(basePos + SETTING_DEBOUNCE);
    specialTrigger = EEPROM.read(basePos + SETTING_TRIGGER);
  } else {
    loadDefaults();
  }
}

void writeSave() {
  EEPROM.write(0, 2); // index 0: save version (here its 2)
  EEPROM.write(1, numKeys);

  for(byte i = 0; i < numKeys; i++) {
    EEPROM.write(2 + i*2, keyModes[i]);
    EEPROM.write(3 + i*2, keyValues[i]);
  }

  byte basePos = 2 + numKeys*2;
  EEPROM.write(basePos + SETTING_DEBOUNCE, debounceCount);
  EEPROM.write(basePos + SETTING_TRIGGER, specialTrigger);
}