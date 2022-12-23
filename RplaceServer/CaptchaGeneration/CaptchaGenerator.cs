using Microsoft.VisualBasic;
using RplaceServer.Types;
using SkiaSharp;

namespace RplaceServer.CaptchaGeneration;

internal static class CaptchaGenerator
{
    private static Random random = new();

    private static readonly string[] Emojis =
    {
        "😎", "🤖", "🗣️", "🔥", "🏠", "🤡", "👾", "👋", "💩", "⚽", "👅", "🧠", "🕶", "🌳", "🌍", "🌈", "🎅", "👶", "👼",
        "🥖", "🍆", "🎮", "🎳", "🚢", "🗿", "ඞ", "📱", "🔑", "❤️", "👺", "🤯", "🤬", "🦩", "🍔", "🎬", "🚨", "⚡️", "🍪",
        "🕋", "🎉", "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "★", "✅", "😂", "😍", "🚀", "😈", "👟", "🍷",
        "🚜", "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "🖱", "😷", "🌱", "🏀", "🛠", "🤮", "💂", "📎",
        "🎄", "🕯️", "🔔", "⛪", "☃️", "🍷", "❄️", "🎁", "🩸"
    };

    private static readonly string[] Strings =
    {
        "rplace", "blobkat", "zekiahepic", "pixels", "game", "donate", "flag", "art", "build", "team", "create", "open",
        "canvas", "board", "anarchy", "reddit", "blank", "colour", "play", "teams", "war", "raid", "make", "learn", "fun"
    };
    
    internal static (string Answer, string? Dummies, byte[] ImageData) Generate(CaptchaType type)
    {
        var answer = "";
        var dummies = new string[10];
            
        switch (type)
        {
            case CaptchaType.Emoji:
                Buffer.BlockCopy(Emojis, random.Next(0, Emojis.Length - 10), dummies, 0, 10);
                answer = dummies[random.Next(0, 10)];
                break;
            case CaptchaType.String:
                Buffer.BlockCopy(Strings, random.Next(0, Emojis.Length - 10), dummies, 0, 10);
                answer = dummies[random.Next(0, 10)];
                break;
            case CaptchaType.Number:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        var bitmap = new SKBitmap(64, 64);
        var canvas = new SKCanvas(bitmap);
        var text = SKTextBlob.Create(answer, new SKFont(SKTypeface.Default, 32, 32));
        canvas.DrawText(text, 0, 0, new SKPaint { Color = SKColors.Black });

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Flush();

        return (answer, string.Join('\n', dummies), stream.ToArray());
    }
}

/*
//import {WebSocketServer} from 'ws'
import im from 'imagemagick'
import imageDataURI from 'image-data-uri'
import fs from 'fs'

const PORT = 1234
let wss = new WebSocketServer({ port: PORT, perMessageDeflate: false })
let answer

wss.on('connection', async function(p, {headers, url: uri}) {
    p.on("error", _=>_)
    p.on('message', async function(data) {
        if (data.toString().split(" ")[0] == "reqImage") {
            await genWordCaptcha()
            p.send("update")
        }
        else if (data.toString().split(" ")[0] == "submit") {
            if (data.toString().split(" ")[1] == answer) {
                p.send("true")
            }
            else {
                p.send("false")
            }
        }
    })
    p.on('close', function(){
    })
})


//Math Captcha
function genMathCaptcha() {
    return new Promise((resolve, reject) => {
        let operation = ["+", "-"][Math.floor(Math.random() * 2)]
        let val = Math.floor(Math.random() * 5), val1 = Math.floor(Math.random() * 5)
        answer = eval(val.toString() + operation.toString() + val1.toString())
        im.convert(['-background', 'white', '-fill', 'black', '-font', 'Candice', '-pointsize', '72', '-wave', `10x${Math.min(Math.max(70 + Math.floor(Math.random() * 10), 70), 80)}`,`label:${val} ${operation} ${val1}`, 'captcha.png'], 
        function(err, stdout){
            if (err) {
                throw err
                reject(err)
            }
            resolve(stdout);
        });
    })
}

function genWordCaptcha() {
    return new Promise((resolve, reject) => { //Allow it to wait for the promise to return before continuing, so we are definite that we 1000% have the new image before we set.
        answer = [][Math.floor(Math.random() * 12)]
        im.convert(['-background', 'white', '-fill', 'black', '-font', 'Candice', '-pointsize', '72', '-wave', `10x${Math.min(Math.max(70 + Math.floor(Math.random() * 10), 70), 80)}`,`label:${answer}`, 'captcha.png'], 
        function(err, stdout){
            if (err) {
                throw err
                reject(err) //Call back and say that it failed
            }
            resolve(stdout); //Finish the async, and allow the program to go on
        });
    })
}


let emojis = ["😎", "🤖", "🗣️", "🔥", "🏠", "🤡", "👾", "👋", "💩", "⚽", "👅", "🧠", "🕶", "🌳", "🌍", "🌈",
        "🥖", "🍆", "🎮", "🎳", "🚢", "🗿", "ඞ", "📱", "🔑", "❤️", "👺", "🤯", "🤬", "🦩", "🍔", "🎬", "🚨", "⚡️",
        "🕋", "🎉", "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "★", "✅", "😂", "😍", "🚀", "😈", "👟",
        "🚜", "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "🖱", "😷", "🌱", "🏀", "🛠", "🤮", "💂", "📎"]
//Emoji captcha
export default function genEmojiCaptcha() {
    return new Promise((resolve, reject) => {
        let answer = emojis[Math.floor(Math.random() * emojis.length)]  //determine answer
        let fileNm = `captcha.${Date.now()}.${Math.floor(Math.random() * 10)}.png` //generate original random file name (we hope)
        im.convert(['-background', ['yellow', 'purple', 'brown', 'white', 'orange', 'blue', 'red', 'pink', 'green', 'black'][Math.floor(Math.random() * 10)],
        `pango:<span size="32384" font="Noto Color Emoji">${answer}</span>`, '-set', 'colorspace', 'sRGB', '-quality', '20',
        '-modulate', `${80 + Math.floor(Math.random()) * 150}, ${80 + Math.floor(Math.random()) * 150}, ${80 + Math.floor(Math.random()) * 150}`,
        '-wave', `8x${Math.min(Math.max(60 + Math.floor(Math.random() * 40), 60), 100)}`,
        '-roll', (Math.random() > 0.5 ? '+' : '-') + Math.floor(Math.random() * 10) + (Math.random() > 0.5 ? '+' : '-') + Math.floor(Math.random() * 10),
        fileNm], //generate emoji
        function(err, stdout){
            if (err) {
                console.log(err)
                reject(err)
            }
            //resolve(stdout);
                imageDataURI.encodeFromFile('./' + fileNm)
                .then(res => { //Encode to png datauri
                        fs.unlink(fileNm, (err) => { if (err) console.error(err)}); //delete the temp saved captcha image
                                let dummies = []
                                let answerPos = Math.floor(Math.random() * 10)
                                for (let i = 0; i < answerPos -1; i++) dummies.push(emojis[Math.floor(Math.random() * emojis.length)]) //pad before
                                dummies.push(answer) //insert our real emoji at this random point in the array
                                for (let j = 0; j < 10-answerPos; j++) dummies.push(emojis[Math.floor(Math.random() * emojis.length)]) //pad after
                        resolve(answer + ' ' + dummies.toString()  + ' ' + res) //return the answer, dummy emojis and captcha as an image data URI to the asker
                })
        })
    })
}
*/